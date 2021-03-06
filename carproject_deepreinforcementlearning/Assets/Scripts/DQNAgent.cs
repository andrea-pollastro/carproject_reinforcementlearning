﻿using MLAgents;
using UnityEngine;
using UnityStandardAssets.Vehicles.Car;
using System.Collections;

public class DQNAgent : Agent
{
    private Rigidbody rigidbody;
    private CarUserControl carUserControl;
    private RaycastController raycastController;
    float minVelocity = 15;
    private bool collided;
    //state
    private float[] state = new float[RaycastController.numRays];

    // Start is called before the first frame update
    void Start()
    {
        //Getting components for driving the car
        carUserControl = GetComponent<CarUserControl>();
        rigidbody = GetComponent<Rigidbody>();
        raycastController = GetComponent<RaycastController>();
    }

    public override void AgentReset()
    {
        //transform.position = new Vector3(234,0,0);//Vector3.zero;
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.Euler(Vector3.zero);
        rigidbody.velocity = Vector3.zero;
        rigidbody.angularVelocity = Vector3.zero;
        raycastController.resetCurrentState();
        collided = false;
    }

    public override void CollectObservations()
    {
        state = raycastController.getCurrentState();
        //raystate
        AddVectorObs(state);
        //car's velocity
        AddVectorObs(normalizeVelocity(transform.InverseTransformDirection(rigidbody.velocity).z));
    }

    private float normalizeVelocity(float x)
    {
        //Needed for having velocity in [0,1]
        return (x - minVelocity)/(150f - minVelocity);
    }

    private void performAction(float steeringAngle, float acceleration)
    {
        carUserControl.setHorizontal(steeringAngle);
        carUserControl.setVertical(acceleration);
        rigidbody.velocity = negativeVelocityLimiter(rigidbody.velocity);
    }

    private Vector3 negativeVelocityLimiter(Vector3 velocity)
    {
        Vector3 localVelocity = transform.InverseTransformDirection(velocity);
        localVelocity.z = localVelocity.z < minVelocity ? minVelocity : localVelocity.z;
        return transform.TransformDirection(localVelocity);
    }

    public override void AgentAction(float[] vectorAction, string textAction)
    {
        float reward;
        float velocity;
        float steeringAngle = vectorAction[0];
        float acceleration = vectorAction[1];

        float w1 = 1.40f;
        float w2 = 1.5f;

        performAction(steeringAngle, acceleration);
        velocity = transform.InverseTransformDirection(rigidbody.velocity).z;
        
        reward = collided ? -10f - w2 * acceleration : 1f + w1 * acceleration;

        SetReward(reward);
        if (collided)
            Done();
    }

    private float getRaysScore()
    {
        float res = 0;
        for (int i = 0; i < state.Length; i++)
            res += state[i];
        return res;
    }
    
    private float getSteeringScore(float steeringAngle, float acceleration)
    {
        float res = 0;
        int startPoint = (steeringAngle * acceleration) < 0 ? 0 : state.Length / 2;
        int endPoint = (steeringAngle * acceleration) < 0 ? state.Length/2 : state.Length;
        for (int i = startPoint; i < endPoint; i++)
            res += state[i];
        res = acceleration > 0 ? Mathf.Abs(steeringAngle) * res : -Mathf.Abs(steeringAngle) * res;
        return res;
    }

    private void OnCollisionEnter(Collision collision)
    {
        collided = true;
    }
}
