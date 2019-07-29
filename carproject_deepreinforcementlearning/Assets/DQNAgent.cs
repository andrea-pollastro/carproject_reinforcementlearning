using MLAgents;
using UnityEngine;
using UnityStandardAssets.Vehicles.Car;
using System.Collections;

public class DQNAgent : Agent
{
    private Rigidbody rigidbody;
    private CarUserControl carUserControl;
    private RaycastController raycastController;
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
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.Euler(Vector3.zero);
        rigidbody.velocity = Vector3.zero;
        rigidbody.angularVelocity = Vector3.zero;
        raycastController.resetCurrentState();
        collided = false;
    }

    public override void CollectObservations()
    {
        //raystate
        AddVectorObs(state);
        //car's velocity
        AddVectorObs(transform.InverseTransformDirection(rigidbody.velocity).z);
    }

    private void performAction(float steeringAngle, float acceleration)
    {
        carUserControl.setHorizontal(steeringAngle);
        carUserControl.setVertical(acceleration);
    }

    public override void AgentAction(float[] vectorAction, string textAction)
    {
        float reward;
        float steeringAngle = vectorAction[0];
        float acceleration = vectorAction[1];
        float accelerationScore;
        float steeringScore;

        performAction(steeringAngle, acceleration);
        //reward calculation
        accelerationScore = acceleration * getRaysScore(); //TODO: controlla se servono solo i centrali
        steeringScore = getSteeringScore(steeringAngle, acceleration);
        reward = collided ? -1000 : accelerationScore + steeringScore;

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
        return res;
    }

    private void OnCollisionEnter(Collision collision)
    {
        collided = true;
    }
}
