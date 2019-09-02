using MLAgents;
using UnityEngine;
using UnityStandardAssets.Vehicles.Car;

public class DQNAgent : Agent
{
    private Rigidbody rigidbody;
    private CarUserControl carUserControl;
    private float minVelocity = 2;
    private bool collided;
    private float w1 = .99f;
    private float w2 = 5f;

    // Start is called before the first frame update
    void Start()
    {
        //Getting components for driving the car
        carUserControl = GetComponent<CarUserControl>();
        rigidbody = GetComponent<Rigidbody>();
    }

    public override void AgentReset()
    {
        transform.position = new Vector3(235,0,0);//Vector3.zero;
        transform.rotation = Quaternion.Euler(Vector3.zero);
        rigidbody.velocity = Vector3.zero;
        rigidbody.angularVelocity = Vector3.zero;
        collided = false;
    }
 
    private float normalizeVelocity(float x)
    {
        //Needed for having velocity in [0,1]
        return (x - minVelocity)/(150f - minVelocity);
    }

    private void performAction(float steeringAngle, float acceleration)
    {
        rigidbody.velocity = negativeVelocityLimiter(rigidbody.velocity);
        carUserControl.setHorizontal(steeringAngle);
        carUserControl.setVertical(acceleration);
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
        float steeringAngle = vectorAction[0];
        float acceleration = vectorAction[1];
        float w1 = .99f;
        float w2 = 5f;

        performAction(steeringAngle, acceleration);

        reward = collided ? -10f - w2 * acceleration : 1f + w1 * acceleration;

        SetReward(reward);
        if (collided)
            Done();
    }

    private void OnCollisionEnter(Collision collision)
    {
        collided = true;
    }
}
