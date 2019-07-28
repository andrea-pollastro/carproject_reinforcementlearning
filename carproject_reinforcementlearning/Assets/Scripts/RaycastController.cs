using UnityEngine;

public class RaycastController : MonoBehaviour
{
    //Objects needed for the rays' description and raycasting
    private static int nRays = 5;
    private float[] maxLenghtRay = new float[nRays];
    private Ray[] rays = new Ray[nRays];
    public static byte numRayIntervals = 4;
    private RaycastHit hit;

    /*
     * Rays mapping:
     * 0: red
     * 1: orange
     * 2: yellow
     * 3: green
     */

    //Physics controller
    private Rigidbody rigidbody;
    private Vector3 localVelocity;
    private bool collided = false;

    //Current state
    private byte[] state = new byte[] { 3, 3, 3, 3, 3 };


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

                    if (res.magnitude < (maxLenghtRay[i] / (numRayIntervals)) + 1.5)
                    {
                        Debug.DrawLine(transform.position, hit.point, Color.red);
                        state[i] = 0;
                    }
                    else if (res.magnitude < (maxLenghtRay[i] / (numRayIntervals)) * 2 + .5)
                    {
                        Debug.DrawLine(transform.position, hit.point, Color.HSVToRGB((float).108, 1, 1));
                        state[i] = 1;
                    }
                    else if (res.magnitude < (maxLenghtRay[i] / (numRayIntervals)) * 3)
                    {
                        Debug.DrawLine(transform.position, hit.point, Color.yellow);
                        state[i] = 2;
                    }
                    else
                    {
                        state[i] = 3;
                    }
                }
            }
        }
    }

    public void resetCurrentState()
    {
        state[0] = state[1] = state[2] = state[3] = state[4] = 3;
        collided = false;
    }

    public byte[] getCurrentState()
    {
        return (byte[])state.Clone();
    }

    private void OnCollisionEnter(Collision collision)
    {
        collided = true;
    }
}
