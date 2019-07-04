using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityStandardAssets.Vehicles.Car;

public class Prova : MonoBehaviour
{
    private UnityStandardAssets.Vehicles.Car.CarUserControl carController;

    public void Start()
    {
        carController = GetComponent<CarUserControl>();
    }

    // Update is called once per frame
    void Update()
    {
        carController.setVertical((float)1);
        carController.setHorizontal(0);
    }
}