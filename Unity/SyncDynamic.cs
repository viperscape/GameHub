using GameNetwork;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// TODO sync remove obj on destroy

[RequireComponent(typeof(Rigidbody))]
public class SyncDynamic : MonoBehaviour
{
    bool isMoving;
    public Kinds kind;
    public bool isRemote = true;
    Vector3 velocity;
    bool isRotating;

    Vector3 lastPos;
    Quaternion lastRot;

    bool needsUpdate;
    public Vector3 nextPos;
    Quaternion nextRot;
    Vector3 nextVel;
    Vector3 nextCMass;

    float lastSync;
    public float dynamicFrequency = 0.06f;
    public float sleepFrequency = 1.5f;
    public uint syncCount; // matches packet id for sync object, discard older updates
    NetManager net;

    // Start is called before the first frame update
    void Start()
    {
        net = GameObject.Find("Game").GetComponent<NetManager>();
    }

    void syncTransform()
    {
        if (isRemote) return;

        var rb = GetComponent<Rigidbody>();

        isMoving = velocity.magnitude > 0 || isRotating || Mathf.Abs(rb.velocity.magnitude) > 0;

        if (Time.time - lastSync > (isMoving ? dynamicFrequency : sleepFrequency))
        {
            lastSync = Time.time;

            Message msg = new Message(Comm.Transform);
            msg.AddUShort((ushort) kind);
            msg.AddString(gameObject.name);

            msg.AddFloat(rb.position.x);
            msg.AddFloat(rb.position.y);
            msg.AddFloat(rb.position.z);

            msg.AddFloat(rb.rotation.x);
            msg.AddFloat(rb.rotation.y);
            msg.AddFloat(rb.rotation.z);
            msg.AddFloat(rb.rotation.w);

            msg.AddFloat(rb.velocity.x);
            msg.AddFloat(rb.velocity.y);
            msg.AddFloat(rb.velocity.z);

            msg.AddFloat(rb.centerOfMass.x);
            msg.AddFloat(rb.centerOfMass.y);
            msg.AddFloat(rb.centerOfMass.z);

            net.SendPlayers(msg, false);

        }
    }

    public void UpdateRigid(Vector3 pos, Quaternion rot, Vector3 vel, Vector3 cMass)
    {
        needsUpdate = true;
        nextPos = pos;
        nextRot = rot;
        nextVel = vel;
        nextCMass = cMass;
    }

    // Update is called once per frame
    void Update()
    {
        velocity = (transform.position - lastPos) / Time.fixedDeltaTime;
        lastPos = transform.position;

        isRotating = transform.rotation.Equals(lastRot);
        lastRot = transform.rotation;
    }

    private void FixedUpdate()
    {
        if (!net.ignoreNetworking)
            syncTransform();

        if (needsUpdate)
        {
            needsUpdate = false;
            var rb = GetComponent<Rigidbody>();
            rb.MovePosition(nextPos);
            rb.MoveRotation(nextRot);
            rb.velocity = nextVel;
            rb.centerOfMass = nextCMass;
        }
    }
}
