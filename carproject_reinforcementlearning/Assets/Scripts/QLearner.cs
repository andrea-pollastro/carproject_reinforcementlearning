using System;
using System.IO;
using System.Collections;
using UnityEngine;
using UnityStandardAssets.Vehicles.Car;


public class QLearner : MonoBehaviour
{
    //hyperparameters
    private int maxSteps = 150;
    private static int numEpisodes = 5000;
    private float[] rewards = new float[numEpisodes];
    private float alpha = .1f;
    private float gamma = .99f;

    //epsilon greedy parameters
    private float epsilon = 1f;
    private float min_exploration_rate = .01f;
    private float max_exploration_rate = 1f;
    private float exploration_decay_rate = .001f;

    //Controller needed to manage the car and to reset it's condition
    private CarUserControl carUserControl;
    private Rigidbody rigidbody;
    private RaycastController raycastController;
    //collision detection flag
    private bool collided = false;

    //needed for choose random number
    private readonly System.Random rand = new System.Random();

    //Actions
    public enum Actions
    {
        left_accelleration,
        left_only,
        left_reverse,
        right_accelleration,
        right_only,
        right_reverse,
        straight_accelleration,
        straight_only,
        straight_reverse
    }
    public static byte numActions = (byte)(Enum.GetValues(typeof(Actions)).Length);

    //Q-table
    private float[,,,,,,] qTable = new float[RaycastController.numRayIntervals,
        RaycastController.numRayIntervals,
        RaycastController.numRayIntervals,
        RaycastController.numRayIntervals,
        RaycastController.numRayIntervals,
        RaycastController.numAccelerationIntervals,
        numActions];

    //Constants used for describe state's elements
    private const byte RAY_SX = 0;
    private const byte RAY_SX_MIDDLE = 1;
    private const byte RAY_MIDDLE = 2;
    private const byte RAY_DX_MIDDLE = 3;
    private const byte RAY_DX = 4;
    private const byte VELOCITY = 5;

    void Start()
    {
        bool learn = true;
        //Getting components for driving the car
        carUserControl = GetComponent<CarUserControl>();
        rigidbody = GetComponent<Rigidbody>();
        raycastController = GetComponent<RaycastController>();

        if (learn) { 
            //Executing the routine for learning
            StartCoroutine(executeLearning());
        }
        else
        {
            readQFunctionValues();
            StartCoroutine(playGame());
        }
    }

    /*
     * LEARNING FUNCTION
     */ 
    public IEnumerator executeLearning()
    {
        float currentReward, randValue;
        byte action;
        
        byte[] state = new byte[6];
        byte[] nextState = new byte[6];

        Vector3 oldPosition, newPosition;
        
        for (int episode = 0; episode < numEpisodes; episode++)
        {
            //init state
            initCarState();
            //init the cycle variables and the initial state
            currentReward = 0;
            state = raycastController.getCurrentState();

            for (int step = 0; step < maxSteps && !collided; step++)
            {
                /*
                 * Epsilon-greedy algorithm: if that rand value is greater than a fixed epsilon, we perform ad exploitation. Instead, we perform 
                 * the exploration
                 * */
                randValue = (rand.Next(0, 1000)) / 1000f;
                if (randValue > epsilon)
                    action = maximize(state);
                else
                    action = (byte)rand.Next(0, 9);
                
                //Once the action is chosen, we can perform it
                performAction(carUserControl, (Actions)action);

                /*
                 * We wait until car doesn't change it's state
                 * For avoiding deadlocks (i.e., if the car has velocity zero and
                 * has "right_only", it's state will never change), we make a check on 
                 * the car's coordinates on the map
                 */
                newPosition = transform.position;
                do
                {
                    //We want to simplify the model since we're in a discrete case
                    rigidbody.velocity = velocityLimiter(rigidbody.velocity);

                    oldPosition = newPosition;
                    yield return null;
                    newPosition = transform.position;
                    if (newPosition == oldPosition)
                        break;
                    nextState = raycastController.getCurrentState();
                } while (compareArrays(state, nextState));

                /*
                 * Bellman equation: needed for approximate the Q-function
                 */
                qTable[state[RAY_SX], state[RAY_SX_MIDDLE], state[RAY_MIDDLE], state[RAY_DX_MIDDLE], state[RAY_DX], state[VELOCITY], action] =
                    qTable[state[RAY_SX], state[RAY_SX_MIDDLE], state[RAY_MIDDLE], state[RAY_DX_MIDDLE], state[RAY_DX], state[VELOCITY], action]
                    * (1 - alpha) + alpha * (reclameReward(nextState) + gamma * maximize(nextState));

                //In the end, we update the state and the current total reward
                state = nextState;
                currentReward += reclameReward(nextState);                
            }

            /*
             * At end of every episode, we save the actual reward for making some analytics on it 
             * and we update the epsilon value
             */
            rewards[episode] = currentReward;
            epsilon = min_exploration_rate + (max_exploration_rate - min_exploration_rate) *
                Mathf.Exp(-exploration_decay_rate * episode);

            Debug.Log("Episodio :" + episode + " Ricompensa: " + currentReward + "Epsilon :" + epsilon);
        }
        //Memorizing the Q-table values and rewards on file
        writeQtableValuesOnFile();
        writeRewardsOnFile();
        //Shutdown
        UnityEditor.EditorApplication.isPlaying = false;
    }

    public IEnumerator playGame()
    {
        Debug.Log("Starting game...");
        byte action;
        byte[] state = new byte[6];
        byte[] nextState = new byte[6];

        Vector3 oldPosition, newPosition;
        for (int episode = 0; episode < numEpisodes; episode++)
        {
            Debug.Log("Starting episode " + episode);
            initCarState();
            state = raycastController.getCurrentState();
            while (!collided)
            {
                action = maximize(state);
                performAction(carUserControl, (Actions)action);

                newPosition = transform.position;
                do
                {
                    rigidbody.velocity = velocityLimiter(rigidbody.velocity);

                    oldPosition = newPosition;
                    yield return null;
                    newPosition = transform.position;
                    if (newPosition == oldPosition)
                        break;
                    nextState = raycastController.getCurrentState();
                } while (compareArrays(state, nextState));

                state = nextState;
            }
        }
    }

    private Vector3 velocityLimiter(Vector3 velocity)
    {
        Vector3 localVelocity = transform.InverseTransformDirection(velocity);
        localVelocity.z = localVelocity.z > 11 ? 11 : localVelocity.z;
        return transform.TransformDirection(localVelocity);
    }

    private void initCarState()
    {
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.Euler(Vector3.zero);
        rigidbody.velocity = Vector3.zero;
        rigidbody.angularVelocity = Vector3.zero;
        raycastController.resetCurrentState();
        collided = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        collided = true;
    }

    private bool compareArrays(byte[] arr1, byte[] arr2)
    {
        for (byte i = 0; i < arr1.Length; i++)
            if (arr1[i] != arr2[i])
                return false;
        return true;
    }

    private byte maximize(byte[] state)
    {
        byte bestIndexValue = 0;
        float bestValue = Single.MinValue;

        for (byte i = 0; i < numActions; i++)
        {
            if (qTable[state[RAY_SX], state[RAY_SX_MIDDLE], state[RAY_MIDDLE], state[RAY_DX_MIDDLE], state[RAY_DX], state[VELOCITY], i] 
                > bestValue)
            {
                bestValue = qTable[state[RAY_SX], state[RAY_SX_MIDDLE], state[RAY_MIDDLE], state[RAY_DX_MIDDLE], state[RAY_DX], state[VELOCITY], i];
                bestIndexValue = i;
            }
        }
        return bestIndexValue;
    }

    private void performAction(CarUserControl carUserControl, Actions action)
    {
        switch (action)
        {
            case Actions.left_accelleration:
                carUserControl.setHorizontal(-0.6f);
                carUserControl.setVertical(0.3f);
                break;
            case Actions.left_only:
                carUserControl.setHorizontal(-0.85f);
                carUserControl.setVertical(0);
                break;
            case Actions.left_reverse:
                carUserControl.setHorizontal(-0.6f);
                carUserControl.setVertical(-0.3f);
                break;
            case Actions.right_accelleration:
                carUserControl.setHorizontal(0.6f);
                carUserControl.setVertical(0.3f);
                break;
            case Actions.right_only:
                carUserControl.setHorizontal(0.85f);
                carUserControl.setVertical(0);
                break;
            case Actions.right_reverse:
                carUserControl.setHorizontal(0.6f);
                carUserControl.setVertical(-0.3f);
                break;
            case Actions.straight_accelleration:
                carUserControl.setHorizontal(0);
                carUserControl.setVertical(0.6f);
                break;
            case Actions.straight_only:
                carUserControl.setHorizontal(0);
                carUserControl.setVertical(0);
                break;
            case Actions.straight_reverse:
                carUserControl.setHorizontal(0);
                carUserControl.setVertical(-0.7f);
                break;
        }
    }

    private float getScore(int stateValue)
    {
        float score = 0;
        switch (stateValue)
        {
            case 4:
                score = 2f;
                break;
            case 3:
                score = 1f;
                break;
            case 2:
                score = -0.5f;
                break;
            case 1:
                score = -1.5f;
                break;
            case 0:
                score = -10000f;
                break;
        }
        return score;
    }
    
    private float reclameReward(byte[] state)
    {
        float velocityReward;
        float sensorReward = getScore(state[RAY_SX]) +
            getScore(state[RAY_SX_MIDDLE]) +
            getScore(state[RAY_MIDDLE]) +
            getScore(state[RAY_DX]) +
            getScore(state[RAY_DX_MIDDLE]);
        /*
         * Acceleration's direction gives a bonus (or a penalty) on the reward.
         * The idea is this one: 
         * if we can run (that means, if we have all rays green or at least one rays yellow), 
         * it means that we must accelerate. For this reason, if the acceleration is positive, we give
         * a bonus, in the other cases we give a penalty.
         * Other cases are similar to this one. 
         * We consider some intervals on the sum of the first 5 rewards to differentiate the cases.
         * The case described above, is the first one (5 < sensorReward <= 10).
         */
        if(sensorReward <= 10 && sensorReward > 5)
        {
            if (state[VELOCITY] == 2)
                velocityReward = 30;
            else
                velocityReward = -50;
        }
        else if(sensorReward <= 5 && sensorReward > -1.5)
        {
            if (state[VELOCITY] == 0)
                velocityReward = 5;
            else
                velocityReward = -10;
        }
        else if(sensorReward <= -1.5 && sensorReward > -5)
        {
            if (state[VELOCITY] == 0)
                velocityReward = 15;
            else
                velocityReward = -30;
        }
        else
        {
            if (state[VELOCITY] == 0)
                velocityReward = 30;
            else
                velocityReward = -50;
        }
        return velocityReward + sensorReward;
    }

    private void writeQtableValuesOnFile()
    {
        String path = Application.dataPath + Path.DirectorySeparatorChar + "qtablevalues.txt";
        if (File.Exists(path))
            File.Delete(path);

        using (TextWriter tw = new StreamWriter(path))
        {
            for (byte a = 0; a < RaycastController.numRayIntervals; a++)
                for (byte b = 0; b < RaycastController.numRayIntervals; b++)
                    for (byte c = 0; c < RaycastController.numRayIntervals; c++)
                        for (byte d = 0; d < RaycastController.numRayIntervals; d++)
                            for (byte e = 0; e < RaycastController.numRayIntervals; e++)
                                for (byte f = 0; f < RaycastController.numAccelerationIntervals; f++)
                                    for(byte g = 0; g < numActions; g++)
                                        tw.WriteLine(qTable[a, b, c, d, e, f, g]);
        }
    }

    private void writeRewardsOnFile()
    {
        String path = Application.dataPath + Path.DirectorySeparatorChar + "rewardsperepisode.txt";
        if (File.Exists(path))
            File.Delete(path);

        using (TextWriter tw = new StreamWriter(path))
        {
            for (int i = 0; i < numEpisodes; i++)
                tw.WriteLine(rewards[i]);
        }
    }

    private void readQFunctionValues()
    {
        String path = Application.dataPath + Path.DirectorySeparatorChar + "qtablevalues.txt";
        string[] lines = File.ReadAllLines(path);
        int index = 0;

        for (byte a = 0; a < RaycastController.numRayIntervals; a++)
            for (byte b = 0; b < RaycastController.numRayIntervals; b++)
                for (byte c = 0; c < RaycastController.numRayIntervals; c++)
                    for (byte d = 0; d < RaycastController.numRayIntervals; d++)
                        for (byte e = 0; e < RaycastController.numRayIntervals; e++)
                            for (byte f = 0; f < RaycastController.numAccelerationIntervals; f++)
                                for (byte g = 0; g < QLearner.numActions; g++)
                                    qTable[a, b, c, d, e, f, g] = float.Parse(lines[index++]);
    }
}
