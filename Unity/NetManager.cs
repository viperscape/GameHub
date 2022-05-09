using Cinemachine;
using GameNetwork;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;


public class NetManager : MonoBehaviour
{
    public bool ignoreNetworking = false; // debug option to spawn player immediately disregarding network server status

    public GameObject Character; // prefab for the player to spawn from

    public GameObject localPlayer; // player character spawned into the local scene/game itself

    public int port = 7070;
    public string host = "broker.1kwgames.com"; // "localhost"; //

    public string area = "test";

    CancellationTokenSource tokenSource;
    public ConcurrentQueue<System.Action> actions;
    public Client client { get; private set; }


    float lastControllerTime;


    // Start is called before the first frame update
    void Start()
    {
        if (ignoreNetworking)
        {
            return;
        }

        lastControllerTime = Time.time;

        actions = new ConcurrentQueue<System.Action>();

        ProgramStart();

        // TODO need to set localPlayer visual effect while waiting
    }

    void ProgramStart()
    {
        tokenSource = new CancellationTokenSource();
        CancellationToken token = tokenSource.Token;
        client = new Client(host, port, token);
        Task.Run(async () => await Program.Start(client, HandleServer, HandlePlayers, token));// NOTE we have to run this in a thread bc Unity seems unreliable
        JoinArea();
    }

    public void JoinArea()
    {
        Message msg = new Message(Comm.JoinGameArea);
        msg.AddString(area);
        msg.AddString(client.localIP);
        _ = client.WriteServer(msg);
    }

    private void FixedUpdate()
    {
        if (ignoreNetworking) return;

        System.Action action;
        while (actions.TryDequeue(out action))
        {
            try
            {
                action();
            }
            catch (System.Exception e)
            {
                Debug.LogError(e);
            }
        }


        // send our controller status
        if (Time.time - lastControllerTime > 0.06 && localPlayer)
        {
            lastControllerTime = Time.time;
            Message msg = new Message(Comm.Controller);
            msg.AddString(localPlayer.name);
            var pc = localPlayer.GetComponent<PlayerController>();
            msg.AddFloat(pc.moveForward);
            msg.AddFloat(pc.moveRight);
            msg.AddFloat(pc.turnRight);

            msg.AddBool(pc.isJumping);
            msg.AddBool(pc.isFiring);
            msg.AddBool(pc.isAiming);
            msg.AddBool(pc.isUsing);
            if (pc.isAiming)
            {
                var obj = pc.GetComponent<ThirdPersonCamera>().followTarget;
                msg.AddFloat(obj.transform.rotation.x);
                msg.AddFloat(obj.transform.rotation.y);
                msg.AddFloat(obj.transform.rotation.z);
                msg.AddFloat(obj.transform.rotation.w);
            }
            msg.AddBool(pc.isSprinting);
            SendPlayers(msg, false);
        }
    }


    async Task HandleServer(Datagram datagram)
    {
        Message msg = new Message(datagram.data);

        System.Action action;

        if (msg.kind == Comm.GameAreasList)
        {
            int count = msg.GetInt();
            for (; count > 0; count--)
            {
                string area = msg.GetString();
            }
        }
        else if (msg.kind == Comm.JoinGameArea)
        {
            bool joined = msg.GetBool();
            if (joined)
            {
                action = () =>
                {
                    SpawnPlayer(client.id.ToString(), false);
                };
                actions.Enqueue(action);
            }
        }
        else if (msg.kind == Comm.BrokerNewMember)
        {
            action = () =>
            {
                ushort id = msg.GetUShort();
                if (GetPlayer(id)) return; // NOTE need to work on reconnects

                SpawnPlayer(id.ToString());
            };
            actions.Enqueue(action);
        }
        else if (msg.kind == Comm.Quit)
        {
            action = () =>
            {
                HandleQuit(msg);
            };
            actions.Enqueue(action);
        }
    }

    GameObject GetPlayer(ushort id)
    {
        var name = localPlayer.GetComponent<SyncDynamic>().kind.ToString() + id;
        return GameObject.Find(name);
    }

    GameObject GetPlayer (Datagram datagram)
    {
        return GetPlayer(datagram.playerId);
    }

    async Task HandlePlayers(Datagram datagram)
    {
        Message msg;
        System.Action action;

        try
        {
            msg = new Message(datagram.data);
        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
            return;
        }

        if (msg.kind == Comm.Transform)
        {
            var kind = (Kinds) msg.GetUShort();
            action = () =>
            {
                string name = msg.GetString();
                var obj = GameObject.Find(name);
                if (!obj)
                {
                    if (kind == Kinds.HookPoint)
                    {
                        var player = GetPlayer(datagram);
                        obj = player.GetComponent<HookController>().hookPoint;
                        if (obj) obj.name = name; // rename based on remote
                    }
                    else if (kind == Kinds.Ballistic)
                    {
                        var player = GetPlayer(datagram);
                    }
                }
                
                if (obj) // redundantly recheck after previous logic
                { 
                    var sync = obj.GetComponent<SyncDynamic>();
                    if (!sync) return;

                    if (sync.syncCount > datagram.packetId) return; // old packet, discard
                    else sync.syncCount = datagram.packetId;

                    var p = new Vector3(msg.GetFloat(), msg.GetFloat(), msg.GetFloat());
                    var r = new Quaternion(msg.GetFloat(), msg.GetFloat(), msg.GetFloat(), msg.GetFloat());
                    var v = new Vector3(msg.GetFloat(), msg.GetFloat(), msg.GetFloat());
                    var m = new Vector3(msg.GetFloat(), msg.GetFloat(), msg.GetFloat());

                    sync.UpdateRigid(p, r, v, m);
                }
            };
            actions.Enqueue(action);
        }
        else if (msg.kind == Comm.Shoot)
        {
            var kind = (Kinds)msg.GetUShort();
            action = () =>
            {
                if (kind == Kinds.HookPoint)
                {
                    var player = GetPlayer(datagram);
                    var from = new Vector3(msg.GetFloat(), msg.GetFloat(), msg.GetFloat());
                    var to = new Vector3(msg.GetFloat(), msg.GetFloat(), msg.GetFloat());
                    player.GetComponent<HookController>().Shoot(from, to, true);
                }
                else if (kind == Kinds.Ballistic)
                {
                    var player = GetPlayer(datagram);
                    var name = msg.GetString();
                    var pos = new Vector3(msg.GetFloat(), msg.GetFloat(), msg.GetFloat()); // TODO do we need player position really?
                    var aim = new Vector3(msg.GetFloat(), msg.GetFloat(), msg.GetFloat());
                    player.GetComponent<Shooter>().Shoot(aim, name);
                }
            };
            actions.Enqueue(action);
        }
        else if (msg.kind == Comm.DetachHook)
        {
            action = () =>
            {
                var player = GetPlayer(datagram);
                player.GetComponent<HookController>().ResetHook(true);
            };
            actions.Enqueue(action);
        }
        else if (msg.kind == Comm.Pickup)
        {
            action = () =>
            {
                var player = GetPlayer(datagram);
                Debug.Log(msg.GetString());
            };
            actions.Enqueue(action);
        }
        else if (msg.kind == Comm.Controller)
        {
            action = () =>
            {
                var name_ = msg.GetString();
                var obj = GameObject.Find(name_);
                if (!obj) return;
                var pc = obj.GetComponent<PlayerController>();
                if (pc.syncCount > datagram.packetId) return; // old packet, discard
                else pc.syncCount = datagram.packetId;


                pc.moveForward = msg.GetFloat();
                pc.moveRight = msg.GetFloat();
                pc.turnRight = msg.GetFloat();

                pc.isJumping = msg.GetBool();
                pc.isFiring = msg.GetBool();
                pc.isAiming = msg.GetBool();
                pc.isUsing = msg.GetBool();
                if (pc.isAiming)
                {
                    var tmp = pc.GetComponent<ThirdPersonCamera>().followTarget;
                    if (tmp) 
                        tmp.transform.rotation = new Quaternion(msg.GetFloat(), msg.GetFloat(), msg.GetFloat(), msg.GetFloat());
                }
                pc.isSprinting = msg.GetBool();
            };
            actions.Enqueue(action);
        }
    }

    void SpawnPlayer(string id, bool isNetPlayer = true)
    {
        Transform spawn = transform;

        if (isNetPlayer)
        {
            GameObject player = Instantiate<GameObject>(Character, spawn.position, spawn.rotation);
            string prefix = player.GetComponent<SyncDynamic>().kind.ToString();
            player.name = prefix + id;
            var pc = player.GetComponent<PlayerController>();
            var camController = player.GetComponent<ThirdPersonCamera>();

            camController.enabled = false;
            pc.isAi = true;
            player.GetComponent<SyncDynamic>().isRemote = true;
        }
        else 
        {
            // NOTE setting spawn is technically not necessary here
            localPlayer.transform.position = spawn.position;
            localPlayer.transform.rotation = spawn.rotation;

            string prefix = localPlayer.GetComponent<SyncDynamic>().kind.ToString();
            localPlayer.name = prefix + id;

            localPlayer.GetComponent<SyncDynamic>().isRemote = false;
        }
    }

    void HandleQuit(Message msg)
    {
        ushort id = msg.GetUShort();
        var obj = GetPlayer(id);
        if (obj) Destroy(obj);
    }

    private void OnDestroy()
    {
        if (tokenSource != null) tokenSource.Cancel();
    }

    public void SendPlayers(Message msg, bool isReliable = true)
    {
        Task.Run(async () => await client.WritePlayers(msg, isReliable));
    }
}
