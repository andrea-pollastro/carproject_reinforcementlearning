using System;
using System.IO;
using System.Collections;
using UnityEngine;
using UnityStandardAssets.Vehicles.Car;


public class QLearner : MonoBehaviour
{
    private byte frame = 0;
    //hyperparameters
    private int T = 150;
    private static int numEpisodes = 5000;
    private float[] rewards = new float[numEpisodes];
    private float alpha = .1f;
    private float gamma = .9f;

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
        float episodeReward;
        float reward;
        byte action;
        
        byte[] state = new byte[6];
        byte[] nextstate = new byte[6];
        
        for (int episode = 0; episode < numEpisodes; episode++)
        {
            //init game
            initCarState();
            episodeReward = 0;
            state = raycastController.getCurrentState();

            for (int t = 0; t < T && !collided; t++)
            {
                //epsilon-greedy algorithm
                action = epsilonGreedy(state);

                //perform action and find new state
                //yield return step(state, nextstate, (Actions)action);
                performAction(carUserControl, (Actions)action);
                while (frame < 15)
                    yield return null;
                frame = 0;
                nextstate = raycastController.getCurrentState();

                reward = reclameReward(state, action, nextstate);
                Debug.Log("Reward: " + reward);
                
                //Bellman equation
                qTable[state[RAY_SX], state[RAY_SX_MIDDLE], state[RAY_MIDDLE], state[RAY_DX_MIDDLE], state[RAY_DX], state[VELOCITY], action] =
                    qTable[state[RAY_SX], state[RAY_SX_MIDDLE], state[RAY_MIDDLE], state[RAY_DX_MIDDLE], state[RAY_DX], state[VELOCITY], action]
                    * (1 - alpha) + alpha * (reward + gamma * maxQValueAction(nextstate));

                //In the end, we update the state and the total reward
                state = nextstate;
                episodeReward += reward;                
            }

            /*
             * At end of every episode, we save the actual reward for making some analytics on it 
             * and we update the epsilon value
             */
            rewards[episode] = episodeReward;
            epsilon = min_exploration_rate + (max_exploration_rate - min_exploration_rate) *
                Mathf.Exp(-exploration_decay_rate * episode);

            Debug.Log("Episodio :" + episode + " Ricompensa: " + episodeReward + "Epsilon :" + epsilon);
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
        byte[] nextstate = new byte[6];
        
        for (int episode = 0; episode < numEpisodes; episode++)
        {
            Debug.Log("Starting episode " + episode);
            initCarState();
            state = raycastController.getCurrentState();
            while (!collided)
            {
                action = findBestAction(state);
                
                Debug.Log("Action: " + (Actions)action);
                performAction(carUserControl, (Actions)action);
                while (frame < 20)
                    yield return null;
                frame = 0;
                nextstate = raycastController.getCurrentState();
                
                //yield return step(state, nextstate, (Actions)action);
                state = nextstate;
            }
        }
    }

    private IEnumerator step(byte[] state, byte[] nextstate, Actions action)
    {
        /*
        * We wait until car doesn't change it's state
        * For avoiding deadlocks (i.e., if the car has velocity zero and
        * has "right_only", it's state will never change), we make a check on 
        * the car's coordinates on the map
        */
        Vector3 oldPosition, newPosition;
        byte[] nextstatetmp = null;
        Debug.Log("\t\tACTION: " + action);
        performAction(carUserControl, action);

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
            nextstatetmp = raycastController.getCurrentState();
        } while (compareArrays(state, nextstatetmp));

        
        for (byte i = 0; i < nextstate.Length; i++)
            if (newPosition != oldPosition)
                nextstate[i] = nextstatetmp[i];
            else
                nextstate[i] = state[i];
    }

    private byte epsilonGreedy(byte[] state)
    {
        /*
        * Epsilon-greedy algorithm: if that rand value is greater than a fixed epsilon, we perform ad exploitation. Instead, we perform 
        * the exploration
        * */
        float randValue;

        randValue = (rand.Next(0, 1000)) / 1000f;
        if (randValue > epsilon)
            return findBestAction(state);
        else
            return (byte)rand.Next(0, 9);
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

    private byte findBestAction(byte[] state)
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

    private float maxQValueAction(byte[] state)
    {
        byte index = findBestAction(state);
        return qTable[state[RAY_SX], state[RAY_SX_MIDDLE], state[RAY_MIDDLE], state[RAY_DX_MIDDLE], state[RAY_DX], state[VELOCITY], index];
    }

    private void performAction(CarUserControl carUserControl, Actions action)
    {
        switch (action)
        {
            case Actions.left_accelleration:
                carUserControl.setHorizontal(-0.65f);
                carUserControl.setVertical(0.3f);
                break;
            case Actions.left_only:
                carUserControl.setHorizontal(-0.90f);
                carUserControl.setVertical(0);
                break;
            case Actions.left_reverse:
                carUserControl.setHorizontal(-0.65f);
                carUserControl.setVertical(-0.35f);
                break;
            case Actions.right_accelleration:
                carUserControl.setHorizontal(0.65f);
                carUserControl.setVertical(0.3f);
                break;
            case Actions.right_only:
                carUserControl.setHorizontal(0.90f);
                carUserControl.setVertical(0);
                break;
            case Actions.right_reverse:
                carUserControl.setHorizontal(0.65f);
                carUserControl.setVertical(-0.35f);
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
    
    private float reclameReward(byte[] state, byte actionIndex, byte[] nextstate)
    {
        float reward;
        float velocityWeight = 1;
        float velocity = transform.InverseTransformDirection(rigidbody.velocity).z;
        velocity = Mathf.Abs(velocity) < 1e-2 ? 0 : velocity;

        float sensorScore = getScore(state[RAY_SX]) +
            getScore(state[RAY_SX_MIDDLE]) +
            getScore(state[RAY_MIDDLE]) +
            getScore(state[RAY_DX]) +
            getScore(state[RAY_DX_MIDDLE]);

        if (sensorScore <= 10 && sensorScore > 5)
            reward = velocity * velocityWeight * 1.5f;
        else
            reward = -velocity * velocityWeight * 2;

        if(collided)
            reward += -10000;

        return reward;
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
        /*
        if(sensorReward <= 10 && sensorReward > 0)
        {
            if (state[VELOCITY] == 2)
                velocityReward = 30;
            else
                velocityReward = -50;
        }
        else /*if(sensorReward <= 5 && sensorReward > -1.5)
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
        else*/
        /*
        {
            if (state[VELOCITY] == 0)
                velocityReward = 30;
            else
                velocityReward = -50;
        }
        */
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
                                    for (byte g = 0; g < numActions; g++)
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
                                for (byte g = 0; g < numActions; g++)
                                    qTable[a, b, c, d, e, f, g] = float.Parse(lines[index++]);
    }

    public void Update()
    {
        frame++;
    }
}
