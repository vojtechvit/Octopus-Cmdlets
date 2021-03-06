﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Octopus.Client.Model;

namespace Octopus_Cmdlets.Tests
{
    [TestClass]
    public class UpdateLibraryVariableTests
    {
        private const string CmdletName = "Update-OctoLibraryVariable";
        private PowerShell _ps;
        private readonly List<LibraryVariableSetResource> _sets = new List<LibraryVariableSetResource>();
        private readonly VariableSetResource _variableSet = new VariableSetResource();

        [TestInitialize]
        public void Init()
        {
            _ps = Utilities.CreatePowerShell(CmdletName, typeof(UpdateLibraryVariable));
            var octoRepo = Utilities.AddOctopusRepo(_ps.Runspace.SessionStateProxy.PSVariable);

            // Create some library variable sets
            _sets.Clear();
            _sets.Add(new LibraryVariableSetResource { Id = "LibraryVariableSets-1", Name = "ConnectionStrings", VariableSetId = "variables-1" });
            _sets.Add(new LibraryVariableSetResource { Id = "LibraryVariableSets-3", Name = "Service Endpoints", VariableSetId = "variables-3" });

            var variable = new VariableResource
            {
                Id = "variables-1",
                Name = "Test",
                Value = "Test Value",
                IsSensitive = false
            };
            variable.Scope.Add(ScopeField.Action, "actions-1");
            variable.Scope.Add(ScopeField.Environment, "environments-1");
            variable.Scope.Add(ScopeField.Role, "DB");

            _variableSet.Variables.Add(variable);

            _sets[0].Links.Add("Variables", "variablesets-1");
            octoRepo.Setup(o => o.LibraryVariableSets.FindOne(It.IsAny<Func<LibraryVariableSetResource, bool>>(), It.IsAny<string>(), It.IsAny<object>()))
                .Returns(
                    (Func<LibraryVariableSetResource, bool> f, string path, object pathParams) =>
                        (from l in _sets where f(l) select l).FirstOrDefault());
            octoRepo.Setup(o => o.Projects.FindByName("Gibberish", null, null)).Returns((ProjectResource)null);

            octoRepo.Setup(o => o.VariableSets.Get("variablesets-1")).Returns(_variableSet);

            var process = new DeploymentProcessResource();
            process.Steps.Add(new DeploymentStepResource { Name = "Website", Id = "Step-1" });
            octoRepo.Setup(o => o.DeploymentProcesses.Get("deploymentprocesses-1")).Returns(process);

            var envs = new List<EnvironmentResource>
            {
                new EnvironmentResource {Id = "environments-1", Name = "DEV"},
                new EnvironmentResource {Id = "environments-2", Name = "TEST"}
            };

            octoRepo.Setup(o => o.Environments.FindByNames(It.IsAny<string[]>(), It.IsAny<string>(), It.IsAny<object>()))
                .Returns((string[] names, string path, object pathParams) => (from n in names
                    from e in envs
                    where e.Name.Equals(n, StringComparison.InvariantCultureIgnoreCase)
                    select e).ToList());

            var machines = new List<MachineResource>
            {
                new MachineResource {Id = "machines-1", Name = "db-01"},
                new MachineResource {Id = "machines-2", Name = "web-01"}
            };
            octoRepo.Setup(o => o.Machines.FindByNames(It.IsAny<string[]>(), It.IsAny<string>(), It.IsAny<object>())).Returns(
                (string[] names, string path, object pathParams) => (from n in names
                                     from m in machines
                                     where m.Name.Equals(n, StringComparison.InvariantCultureIgnoreCase)
                                     select m).ToList());
        }

        [TestMethod, ExpectedException(typeof(ParameterBindingException))]
        public void No_Arguments()
        {
            // Execute cmdlet
            _ps.AddCommand(CmdletName);
            _ps.Invoke();
        }

        [TestMethod, ExpectedException(typeof(ParameterBindingException))]
        public void No_Id()
        {
            // Execute cmdlet
            _ps.AddCommand(CmdletName).AddParameter("VariableSet", "ConnectionStrings").AddParameter("Name", "Test");
            _ps.Invoke();
        }

        [TestMethod, ExpectedException(typeof(ParameterBindingException))]
        public void No_Project()
        {
            // Execute cmdlet
            _ps.AddCommand(CmdletName).AddParameter("Id", "variables-1").AddParameter("Name", "Test");
            _ps.Invoke();
        }

        [TestMethod]
        public void With_Name()
        {
            // Execute cmdlet
            _ps.AddCommand(CmdletName).AddParameter("VariableSet", "ConnectionStrings").AddParameter("Id", "variables-1").AddParameter("Name", "NewName");
            _ps.Invoke();

            Assert.AreEqual("NewName", _variableSet.Variables[0].Name);
        }

        [TestMethod]
        public void With_Sensitive()
        {
            // Execute cmdlet
            _ps.AddCommand(CmdletName).AddParameter("VariableSet", "ConnectionStrings").AddParameter("Id", "variables-1").AddParameter("Sensitive", true);
            _ps.Invoke();

            Assert.AreEqual(true, _variableSet.Variables[0].IsSensitive);
        }

        [TestMethod]
        public void With_Environments()
        {
            // Execute cmdlet
            _ps.AddCommand(CmdletName).AddParameter("VariableSet", "ConnectionStrings").AddParameter("Id", "variables-1").AddParameter("Environment", "TEST");
            _ps.Invoke();

            Assert.AreEqual("environments-2", _variableSet.Variables[0].Scope[ScopeField.Environment].First());
        }

        [TestMethod, ExpectedException(typeof(CmdletInvocationException))]
        public void With_Invalid_Project()
        {
            // Execute cmdlet
            _ps.AddCommand(CmdletName).AddParameter("VariableSet", "Gibberish").AddParameter("Id", "variables-1");
            _ps.Invoke();
        }

        [TestMethod, ExpectedException(typeof(CmdletInvocationException))]
        public void With_Invalid_Id()
        {
            // Execute cmdlet
            _ps.AddCommand(CmdletName).AddParameter("VariableSet", "ConnectionStrings").AddParameter("Id", "Gibberish");
            _ps.Invoke();
        }

        [TestMethod]
        public void With_All()
        {
            // Execute cmdlet
            _ps.AddCommand(CmdletName)
                .AddParameter("VariableSet", "ConnectionStrings")
                .AddParameter("Id", "variables-1")
                .AddParameter("Name", "NewName")
                .AddParameter("Value", "New Test Value")
                .AddParameter("Environments", new[] { "TEST" })
                .AddParameter("Roles", new[] { "Web" })
                .AddParameter("Machines", new[] { "web-01" })
                .AddParameter("Sensitive", true);
            _ps.Invoke();

            Assert.AreEqual(1, _variableSet.Variables.Count);
            Assert.AreEqual("NewName", _variableSet.Variables[0].Name);
            Assert.AreEqual("New Test Value", _variableSet.Variables[0].Value);
            Assert.AreEqual(true, _variableSet.Variables[0].IsSensitive);
            Assert.AreEqual("environments-2", _variableSet.Variables[0].Scope[ScopeField.Environment].First());
            Assert.AreEqual("Web", _variableSet.Variables[0].Scope[ScopeField.Role].First());
            Assert.AreEqual("machines-2", _variableSet.Variables[0].Scope[ScopeField.Machine].First());
        }

        //[TestMethod]
        //public void With_Object()
        //{
        //    // Execute cmdlet
        //    _ps.AddCommand(CmdletName)
        //        .AddParameter("Project", "Octopus")
        //        .AddParameter("InputObject", new VariableResource { Name = "Test" });
        //    _ps.Invoke();

        //    Assert.AreEqual(1, _variableSet.Variables.Count);
        //    Assert.AreEqual("Test", _variableSet.Variables[0].Name);
        //}
    }
}
