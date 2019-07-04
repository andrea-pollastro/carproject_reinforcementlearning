using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityStandardAssets.Vehicles.Car;

public class QLearner : MonoBehaviour
{
    //Controller needed to reset the condition to starting condition
    private CarUserControl carUserControl;
    private Rigidbody rigidbody;
    private RaycastController raycastController;

    //Q-table
    private float[,,,,,] qTable = new float[4,4,4,4,4,9];
    
    //hyperparameters
    private int episodes = 1000;
    private float[] rewards = new float[1000];
    private int maxSteps = 100;
    private float alpha = .5f;
    private float gamma = .99f;
    //epsilon greedy parameters
    private float epsilon = 1f;
    private float min_exploration_rate = .01f;
    private float max_exploration_rate = 1f;
    private float exploration_decay_rate = .01f;

    //needed for random
    private readonly System.Random rand = new System.Random();

    //Initial GameObject state
    private Vector3 initPosition;
    private Quaternion initOrientation;

    // Start is called before the first frame update
    void Start()
    {
        //getting needed components
        carUserControl = GetComponent<CarUserControl>();
        rigidbody = GetComponent<Rigidbody>();
        raycastController = GetComponent<RaycastController>();
        //saving init info
        initPosition = transform.position;
        initOrientation = transform.rotation;
        //start q-function learning
        executeLearning();
    }

    public void executeLearning()
    {
        float currentReward;
        float randValue;
        byte action;
        byte[] state = new byte[5];

        for(int episode = 0; episode < episodes; episode++)
        {
            //reinit state
            transform.position = initPosition;
            transform.rotation = initOrientation;
            rigidbody.velocity = Vector3.zero;
            currentReward = 0;
            
            for(int step = 0; step < maxSteps; step++)
            {
                state = raycastController.getCurrentState(state);
                randValue = (float)((rand.Next(0, 1000))/1000);
                if(randValue > epsilon)
                {
                    //exploitation
                    action = max(qTable, state[0], state[1], state[2], state[3], state[4]);
                }
                else
                {
                    //exploration
                    action = (byte)rand.Next(0, 9);
                }

                //TODO: Bellman equation
            }
        }
    }

    private byte max(float[,,,,,] qTable, short s1, short s2, short s3, short s4, short s5)
    {
        byte bestIndexValue = 0;
        float bestValue = 0;
        for(byte i = 0; i < 9; i++)
        {
            if(qTable[s1, s2, s3, s4, s5, i] > bestValue)
            {
                bestValue = qTable[s1, s2, s3, s4, s5, i];
                bestIndexValue = i;
            }
        }
        return bestIndexValue;
    }

}
