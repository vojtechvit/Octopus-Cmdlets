﻿using System;
using System.Linq;
using System.Management.Automation;
using Octopus.Client;
using Octopus.Client.Model;

namespace Octopus.Cmdlets
{
    [Cmdlet(VerbsCommon.Remove, "Variable", DefaultParameterSetName = "ByProjectName")]
    public class RemoveVariable : PSCmdlet
    {
        [Parameter(
            ParameterSetName = "ByProjectName",
            Position = 0,
            Mandatory = true,
            HelpMessage = "The project to remove the variables from.")]
        [Alias("ProjectName")]
        public string Project { get; set; }

        [Parameter(
            Position = 1,
            Mandatory = true,
            ValueFromPipelineByPropertyName = true,
            ValueFromPipeline = true,
            HelpMessage = "The name of the variable to remove.")]
        public string Name { get; set; }

        private OctopusRepository _octopus;
        private VariableSetResource _variableSet;

        protected override void BeginProcessing()
        {
            _octopus = (OctopusRepository)SessionState.PSVariable.GetValue("OctopusRepository");
            if (_octopus == null)
            {
                throw new Exception(
                    "Connection not established. Please connect to your Octopus Deploy instance with Connect-OctoServer");
            }

            // Find the project that owns the variables we want to edit
            var project = _octopus.Projects.FindByName(Project);

            if (project == null)
            {
                const string msg = "Project '{0}' was found.";
                throw new Exception(string.Format(msg, Project));
            }

            // Get the variables for editing
            _variableSet = _octopus.VariableSets.Get(project.Link("Variables"));
        }

        protected override void ProcessRecord()
        {
            // This is debatable. If you have more than one of the same name,
            // it's going to remove one at random. On the other hand, if you 
            // get all matching the name, put pipe the name in multiple times 
            // (ie: find Test.*) you'll get an error on the successive attempts.
            var variable = _variableSet.Variables.FirstOrDefault(x => x.Name == Name);

            if (variable == null)
            {
                const string msg = "No variable with the name '{0}' in the project '{1}' was found.";
                //throw new Exception(string.Format(msg, Name, Project));
                WriteWarning(string.Format(msg, Name, Project));
            }
            else
            {
                const string msg = "Removing variable '{0}' from project '{1}'";
                WriteVerbose(string.Format(msg, variable.Name, Project));
                _variableSet.Variables.Remove(variable);
            }
        }

        protected override void EndProcessing()
        {
            // Save the variables
            _octopus.VariableSets.Modify(_variableSet);

            WriteDebug("Modified the variable set");
        }
    }
}
