using UnityEngine;

public class RaycastController : MonoBehaviour
{
    //Objects needed for the rays' description and raycasting
    private static int nRays = 5;
    private float[] maxLenghtRay = new float[nRays];
    private Ray[] rays = new Ray[nRays];
    public static byte numRayIntervals = 5;
    public static byte numAccelerationIntervals = 3;
    private RaycastHit hit;

    /*
     * Acceleration mapping:
     * 0: negative acceleration
     * 1: zero acceleration
     * 2: positive acceleration
     * (this mapping is useful for accessing to the Q-table matrix, for this reason we didn't use -1,0,1)
     * 
     * Rays mapping:
     * 0: collision
     * 1: red
     * 2: orange
     * 3: yellow
     * 4: green
     */

    //Physics controller
    private Rigidbody rigidbody;
    private Vector3 localVelocity;
    private bool collided = false;
    
    //Current state
    private byte[] state = new byte[] { 4, 4, 4, 4, 4, 0 };

    void Start()
    {
        maxLenghtRay[0] = 15f;
        maxLenghtRay[1] = 20f;
        maxLenghtRay[2] = 30f;
        maxLenghtRay[3] = 20f;
        maxLenghtRay[4] = 15f;
        rigidbody = GetComponent<Rigidbody>();
    }

    //callback pre-frame
    void FixedUpdate()
    {
        if (!collided)
        {
            //Angle coverage: 120 degrees
            int angle = -40;

            //Rays creation
            for (byte i = 0; i < nRays; i++)
            {
                rays[i] = new Ray(transform.position, Quaternion.AngleAxis(angle, Vector3.up) * transform.forward);
                angle += 20;
            }

            //Rays drawing
            for (byte i = 0; i < nRays; i++)
                Debug.DrawRay(rays[i].origin, rays[i].direction * maxLenghtRay[i], Color.green);

            //Raycasting
            for (byte i = 0; i < nRays; i++)
            {
                if (Physics.Raycast(rays[i], out hit, maxLenghtRay[i]))
                {
                    Vector3 res = hit.point - transform.position;

                    if (res.magnitude < (maxLenghtRay[i] / (numRayIntervals - 1)) + 1.5)
                    {
                        Debug.DrawLine(transform.position, hit.point, Color.red);
                        state[i] = 1;
                    }
                    else if (res.magnitude < (maxLenghtRay[i] / (numRayIntervals - 1)) * 2 + .5)
                    {
                        Debug.DrawLine(transform.position, hit.point, Color.HSVToRGB((float).108, 1, 1));
                        state[i] = 2;
                    }
                    else if (res.magnitude < (maxLenghtRay[i] / (numRayIntervals - 1)) * 3)
                    {
                        Debug.DrawLine(transform.position, hit.point, Color.yellow);
                        state[i] = 3;
                    }
                    else
                    {
                        state[i] = 4;
                    }
                }
            }
            state[5] = detectVelocityDirection(transform.InverseTransformDirection(rigidbody.velocity).z);
        }
    }

    public void resetCurrentState()
    {
        state[0] = state[1] = state[2] = state[3] = state[4] = 4;
        state[5] = 0;
        collided = false;
    }

    public byte[] getCurrentState()
    {
        return (byte[])state.Clone();
    }

    private void OnCollisionEnter(Collision collision)
    {
        collided = true;
        byte minIndex = 0;
        for (byte i = 1; i < nRays; i++)
        {
            if (state[i] < state[minIndex])
                minIndex = i;
        }
        state[minIndex] = 0;
    }

    private byte detectVelocityDirection(float vel)
    {
        if (Mathf.Abs(vel) < 1e-2)
            return 1;
        else
            return (byte)(vel > 0 ? 2 : 0);
    }
}
