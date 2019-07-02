using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RaycastController : MonoBehaviour
{
    public float maxLenghtRay = 25;
    private Ray[] rays = new Ray[5];
    //Oggetto necessario per trovare l'intersezione dei raggi con la scena
    private RaycastHit hit;


    //callback pre-frame
    void FixedUpdate()
    {
        //Angolo di apertura totale: 120
        int angle = -40;
        
        //Creazione raggi
        for(int i = 0; i < rays.Length; i++){
            rays[i] = new Ray(transform.position, Quaternion.AngleAxis(angle, Vector3.up) * transform.forward);
            angle += 20;
        }
        
        //Disegno raggi
        foreach(Ray ray in rays)
            Debug.DrawRay(ray.origin, ray.direction * maxLenghtRay, Color.green);
        
        //Raycasting
        foreach(Ray ray in rays){
            if (Physics.Raycast(ray, out hit, maxLenghtRay)){
                Debug.DrawLine(transform.position, hit.point, Color.red);
            }
        }
    }
}
