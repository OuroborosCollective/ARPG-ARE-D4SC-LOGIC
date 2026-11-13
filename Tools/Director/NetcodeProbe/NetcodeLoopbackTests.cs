using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;

// Copy only into the isolated tooling probe; the Ashen Crypt game has no Netcode dependency.
public sealed class NetcodeLoopbackTests
{
    private GameObject _root;
    private NetworkManager _manager;

    [UnityTest]
    public IEnumerator LocalHostStartsStopsAndCanRestart()
    {
        Assert.IsNull(NetworkManager.Singleton);
        _root = new GameObject("Official Netcode isolated probe");
        var transport = _root.AddComponent<UnityTransport>();
        transport.SetConnectionData(true, "127.0.0.1", 17853, "127.0.0.1");
        _manager = _root.AddComponent<NetworkManager>();
        _manager.NetworkConfig = new NetworkConfig
        {
            NetworkTransport = transport,
            EnableSceneManagement = false
        };

        for (int cycle = 0; cycle < 2; cycle++)
        {
            Assert.IsTrue(_manager.StartHost(), "Host start failed on cycle " + cycle);
            yield return null;
            Assert.IsTrue(_manager.IsListening);
            Assert.IsTrue(_manager.IsHost);
            Assert.IsTrue(_manager.IsConnectedClient);
            Assert.AreEqual(1, _manager.ConnectedClients.Count);
            _manager.Shutdown();
            for (int frame = 0; frame < 120 && _manager.IsListening; frame++) yield return null;
            Assert.IsFalse(_manager.IsListening, "Host shutdown did not complete.");
        }
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (_manager != null)
        {
            _manager.Shutdown();
            for (int frame = 0; frame < 120 && _manager.IsListening; frame++) yield return null;
        }
        if (_root != null) Object.Destroy(_root);
        yield return null;
        Assert.IsNull(NetworkManager.Singleton, "Probe must release its NetworkManager.");
    }
}
