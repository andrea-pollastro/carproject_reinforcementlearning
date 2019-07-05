using System;
using UnityEngine;
using UnityStandardAssets.CrossPlatformInput;

namespace UnityStandardAssets.Vehicles.Car
{
    [RequireComponent(typeof (CarController))]
    public class CarUserControl : MonoBehaviour
    {
        private CarController m_Car; // the car controller we want to use
        private float horizontal = 0;
        private float vertical = 0;
   

        private void Awake()
        {
            // get the car controller
            m_Car = GetComponent<CarController>();
        }

        public void setHorizontal(float horizontal)
        {
            this.horizontal = horizontal;
        }

        public void setVertical(float vertical)
        {
            this.vertical = vertical;
        }

        private void FixedUpdate()
        {
            // pass the input to the car!
            //float h = CrossPlatformInputManager.GetAxis("Horizontal");
            //float v = CrossPlatformInputManager.GetAxis("Vertical");
#if !MOBILE_INPUT
            float handbrake = CrossPlatformInputManager.GetAxis("Jump");

            m_Car.Move(horizontal, vertical, vertical, handbrake);
#else
            m_Car.Move(horizontal, vertical, vertical, 0f);
#endif
        }
    }
}
