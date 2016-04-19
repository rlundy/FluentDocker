﻿using System;
using System.Diagnostics;
using System.Linq;
using Ductus.FluentDocker.Commands;
using Ductus.FluentDocker.Extensions;
using Ductus.FluentDocker.Model;
using Ductus.FluentDocker.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ductus.FluentDockerTest
{
  [TestClass]
  public class DockerClientCommandTests
  {
    private static string _caCertPath;
    private static string _clientCertPath;
    private static string _clientKeyPath;
    private static CertificatePaths _certificates;
    private static Uri _docker;
    private static bool _createdTestMachine;

    [ClassInitialize]
    public static void Initialize(TestContext ctx)
    {
      var machineName = "test-machine";

      var machines = Machine.Ls();
      if (machines.Success && machines.Data.Any(x => x.Name == "default"))
      {
        machineName = "default";
      }
      else
      {
        machineName.Create(1024, 20000, 1);
        _createdTestMachine = true;
      }

      machineName.Start();
      var inspect = machineName.Inspect().Data;

      _docker = machineName.Uri();
      _caCertPath = inspect.AuthConfig.CaCertPath;
      _clientCertPath = inspect.AuthConfig.ClientCertPath;
      _clientKeyPath = inspect.AuthConfig.ClientKeyPath;

      _certificates = new CertificatePaths
      {
        CaCertificate = _caCertPath,
        ClientCertificate = _clientCertPath,
        ClientKey = _clientKeyPath
      };
    }

    [ClassCleanup]
    public static void TearDown()
    {
      if (_createdTestMachine)
      {
        "test-machine".Delete(true /*force*/);
      }
    }

    [TestMethod]
    public void RunWithoutArgumentShallSucceed()
    {
      string id = null;
      try
      {
        var cmd =_docker.Run("nginx:latest", null, _caCertPath, _clientCertPath, _clientKeyPath);
        Assert.IsTrue(cmd.Success);

        id = cmd.Data;
        Assert.IsTrue(!string.IsNullOrWhiteSpace(id));
        Assert.AreEqual(64, id.Length);
      }
      finally
      {
        if (null != id)
        {
          _docker.RemoveContainer(id, true, true, null, _caCertPath, _clientCertPath, _clientKeyPath);
        }
      }
    }

    [TestMethod]
    public void RemoveContainerShallSucceed()
    {
      string id = null;
      try
      {
        var cmd = _docker.Run("nginx:latest", null, _caCertPath, _clientCertPath, _clientKeyPath);
        Assert.IsTrue(cmd.Success);

        id = cmd.Data;
        Assert.IsTrue(!string.IsNullOrWhiteSpace(id));
        Assert.AreEqual(64, id.Length);

        var rm = _docker.RemoveContainer(id, true, true, null, _caCertPath, _clientCertPath, _clientKeyPath);
        Assert.IsTrue(rm.Success);
        Assert.AreEqual(id, rm.Data);
      }
      finally
      {
        if (null != id)
        {
          _docker.RemoveContainer(id, true, true, null, _caCertPath, _clientCertPath, _clientKeyPath);
        }
      }

    }


    [TestMethod]
    public void DockerPsWithOneContainerShallGiveOneResult()
    {
      string id = null;
      try
      {
        var cmd = _docker.Run("nginx:latest", null, _caCertPath, _clientCertPath, _clientKeyPath);
        Assert.IsTrue(cmd.Success);

        id = cmd.Data;
        Assert.IsTrue(!string.IsNullOrWhiteSpace(id));
        Assert.AreEqual(64, id.Length);

        var ls = _docker.Ps(null, _caCertPath, _clientCertPath, _clientKeyPath);
        Assert.IsTrue(ls.Success);
        Assert.AreEqual(1,ls.Data.Count);
        Assert.IsTrue(id.StartsWith(ls.Data[0]));
      }
      finally
      {
        if (null != id)
        {
          _docker.RemoveContainer(id, true, true, null, _caCertPath, _clientCertPath, _clientKeyPath);
        }
      }
    }

    [TestMethod]
    public void InspectRunningContainerShallSucceed()
    {
      string id = null;
      try
      {
        var cmd = _docker.Run("nginx:latest", null, _caCertPath, _clientCertPath, _clientKeyPath);
        Assert.IsTrue(cmd.Success);

        id = cmd.Data;
        Assert.IsTrue(!string.IsNullOrWhiteSpace(id));
        Assert.AreEqual(64, id.Length);

        var inspect = _docker.InspectContainer(id, _caCertPath, _clientCertPath, _clientKeyPath);
        Assert.IsTrue(inspect.Success);
        Assert.IsTrue(inspect.Data.Name.Length > 2);
        Assert.AreEqual(true, inspect.Data.State.Running);
      }
      finally
      {
        if (null != id)
        {
          _docker.RemoveContainer(id, true, true, null, _caCertPath, _clientCertPath, _clientKeyPath);
        }
      }
    }

    [TestMethod]
    public void RunPostgresContainerAndCheckThatAllProcessesAreRunning()
    {
      string id = null;
      try
      {
        var cmd = _docker.Run("postgres:latest", new ContainerCreateParams
        {
          PortMappings = new[] {"40001:5432"},
          Environment = new[] {"POSTGRES_PASSWORD=mysecretpassword"}
        }, _certificates);

        Assert.IsTrue(cmd.Success);

        id = cmd.Data;
        Assert.IsTrue(!string.IsNullOrWhiteSpace(id));
        Assert.AreEqual(64, id.Length);

        var config = _docker.InspectContainer(id, _certificates);
        Assert.IsTrue(config.Success);
        Assert.AreEqual(true, config.Data.State.Running);

        var endpoint = config.Data.NetworkSettings.Ports.ToHostPort("5432/tcp", _docker);
        endpoint.WaitForPort(10000 /*10s*/);

        var ls = _docker.Top(id, _certificates);
        Assert.IsTrue(ls.Success);

        var proc = ls.Data;
        Debug.WriteLine(proc.ToString());

        Assert.AreEqual(6, ls.Data.Rows.Count);
        Assert.IsTrue(proc.Rows.Any(x => x.Command == "postgres"));
        Assert.IsTrue(proc.Rows.Any(x => x.Command == "postgres: checkpointer process"));
        Assert.IsTrue(proc.Rows.Any(x => x.Command == "postgres: writer process"));
        Assert.IsTrue(proc.Rows.Any(x => x.Command == "postgres: wal writer process"));
        Assert.IsTrue(proc.Rows.Any(x => x.Command == "postgres: autovacuum launcher process"));
        Assert.IsTrue(proc.Rows.Any(x => x.Command == "postgres: stats collector process"));
      }
      finally
      {
        if (null != id)
        {
          _docker.RemoveContainer(id, true, true, null, _certificates);
        }
      }
    }
  }
}