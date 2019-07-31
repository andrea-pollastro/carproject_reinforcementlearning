using UnityEngine;

public class RaycastController : MonoBehaviour
{
    //Objects needed for the rays' description and raycasting
    public static int numRays = 17;
    private float[] lengthRay = new float[numRays];
    private Ray[] rays = new Ray[numRays];
    private RaycastHit hit;

    //Physics controller
    private Rigidbody rigidbody;
    private Vector3 localVelocity;
    private bool collided = false;

    //Current state
    private float[] state = new float[numRays];


    void Start()
    {
        initState();
        defineRayLength();
        rigidbody = GetComponent<Rigidbody>();
    }

    private void defineRayLength()
    {
        int halfRays = numRays / 2;
        float maxLength = 35f;
        float minLength = 15f;
        float diff = (maxLength - minLength) / (halfRays + 1);

        lengthRay[halfRays] = 30f;
        for (int i = 1; i < halfRays; i++)
            lengthRay[halfRays + i] = lengthRay[halfRays - i] = lengthRay[halfRays] - (diff * i);
    }

    private void initState()
    {
        for(int i = 0; i < state.Length; i++)
            state[i] = 1;
    }

    //callback pre-frame
    void FixedUpdate()
    {
        if (!collided)
        {
            //Angle coverage: 120 degrees
            int angle = -40;

            //Rays creation
            for (byte i = 0; i < numRays; i++)
            {
                rays[i] = new Ray(transform.position, Quaternion.AngleAxis(angle, Vector3.up) * transform.forward);
                angle += 5;
            }

            //Rays drawing
            for (byte i = 0; i < numRays; i++)
                Debug.DrawRay(rays[i].origin, rays[i].direction * lengthRay[i], Color.green);

            //Raycasting
            for (byte i = 0; i < numRays; i++)
            {
                if (Physics.Raycast(rays[i], out hit, lengthRay[i]))
                {
                    Vector3 res = hit.point - transform.position;
                    state[i] = transformHitPoint(res.magnitude, lengthRay[i]);
                    Debug.DrawLine(transform.position, hit.point, Color.HSVToRGB(0, 1, res.magnitude/lengthRay[i]));
                }
            }
        }
    }

    private float transformHitPoint(float k, float x)
    {
        //Codomain: [-1,1] now, [-1.5,.5]
        return (2 * (k / x) - 1) - 0.5f;
    }

    public void resetCurrentState()
    {
        initState();
        collided = false;
    }

    public float[] getCurrentState()
    {
        return (float[])state.Clone();
    }

    private void OnCollisionEnter(Collision collision)
    {
        collided = true;
    }
}