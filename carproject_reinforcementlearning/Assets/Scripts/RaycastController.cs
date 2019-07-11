using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RaycastController : MonoBehaviour
{
    //Variables for the ray
    private static int nRays = 5;
    private float[] maxLenghtRay = new float[nRays];
    private Ray[] rays = new Ray[nRays];
    private byte numZones = 4;
    private bool collided = false;
    private Rigidbody rigidbody;

    //Oggetto necessario per trovare l'intersezione dei raggi con la scena
    private RaycastHit hit;
    private byte[] currentState = new byte[7];

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
        //Angolo di apertura totale: 120
        int angle = -40;

        //Creazione raggi
        for(byte i = 0; i < nRays; i++){
            rays[i] = new Ray(transform.position, Quaternion.AngleAxis(angle, Vector3.up) * transform.forward);
            angle += 20;
            currentState[i] = 4;
        }

        //Disegno raggi
        for(byte i = 0; i < nRays; i++)
            Debug.DrawRay(rays[i].origin, rays[i].direction * maxLenghtRay[i], Color.green);
        
        //Raycasting
        for(byte i = 0; i < nRays; i++){
            if (Physics.Raycast(rays[i], out hit, maxLenghtRay[i])){
                Vector3 res = hit.point - transform.position;

                if (res.magnitude < (maxLenghtRay[i]/numZones))
                {
                    Debug.DrawLine(transform.position, hit.point, Color.red);
                    currentState[i] = 1;
                }
                else if (res.magnitude < (maxLenghtRay[i] / numZones)*2)
                {
                    Debug.DrawLine(transform.position, hit.point, Color.HSVToRGB((float).108, 1, 1));
                    currentState[i] = 2;
                }
                else if(res.magnitude < (maxLenghtRay[i] / numZones) * 3)
                {
                    Debug.DrawLine(transform.position, hit.point, Color.yellow);
                    currentState[i] = 3;
                }
                else
                {
                    Debug.DrawLine(transform.position, hit.point, Color.green);
                    currentState[i] = 4;
                }
            }
        }

        currentState[5] = (byte)normalize(transform.InverseTransformDirection(rigidbody.velocity).x);//rigidbody.velocity.x);
        currentState[6] = (byte)normalize(transform.InverseTransformDirection(rigidbody.velocity).z);//rigidbody.velocity.z);
    }

    public byte[] getCurrentState()
    {
        byte[] state = new byte[currentState.Length];

        for (byte i = 0; i < currentState.Length; i++)
            state[i] = currentState[i];
        return state;
    }

    private void OnCollisionEnter(Collision collision)
    {
        byte minIndex = 0;
        byte minValue = 0;
        collided = true;
        for (byte i = 1; i < currentState.Length; i++)
        {
            if (currentState[i] < currentState[minIndex])
                minIndex = i;
        }
        currentState[minIndex] = 0;
    }

    private int normalize(float vel)
    {
        
        if (vel > 8)
            return 4;
        else if (vel > 4)
            return 3;
        else if (vel > 0)
            return 2;
        else if (vel > -2)
            return 1;
        else 
            return 0;
    }
}
