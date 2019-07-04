using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RaycastController : MonoBehaviour
{
    public float maxLenghtRay = 25;
    private Ray[] rays = new Ray[5];
    //Oggetto necessario per trovare l'intersezione dei raggi con la scena
    private RaycastHit hit;
    private byte[] currentState = new byte[5];


    public byte[] getCurrentState(byte[] qlearnerState)
    {
        for (byte i = 0; i < currentState.Length; i++)
            qlearnerState[i] = currentState[i];
        return qlearnerState;
    }
    //callback pre-frame
    void FixedUpdate()
    {
        //Angolo di apertura totale: 120
        int angle = -40;

        //Creazione raggi
        for(byte i = 0; i < rays.Length; i++){
            rays[i] = new Ray(transform.position, Quaternion.AngleAxis(angle, Vector3.up) * transform.forward);
            angle += 20;
            currentState[i] = 3;
        }

        //Disegno raggi
        foreach(Ray ray in rays)
            Debug.DrawRay(ray.origin, ray.direction * maxLenghtRay, Color.green);
        
        //Raycasting
        for(byte i = 0; i < rays.Length; i++){
            if (Physics.Raycast(rays[i], out hit, maxLenghtRay)){
                Vector3 res = hit.point - transform.position;
                if (res.magnitude < 4)
                {
                    Debug.DrawLine(transform.position, hit.point, Color.red);
                    currentState[i] = 0;
                }
                else if (res.magnitude < 8)
                {
                    Debug.DrawLine(transform.position, hit.point, Color.HSVToRGB((float).108, 1, 1));
                    currentState[i] = 1;
                }
                else
                {
                    Debug.DrawLine(transform.position, hit.point, Color.yellow);
                    currentState[i] = 2;
                }
            }
        }
    }
}
