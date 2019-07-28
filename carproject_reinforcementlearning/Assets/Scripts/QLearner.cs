using System;
using System.IO;
using System.Collections;
using UnityEngine;
using UnityStandardAssets.Vehicles.Car;


public class QLearner : MonoBehaviour
{
    //frame counter
    private byte frame = 0;
    //hyperparameters
    private int T = 150;
    private static int numEpisodes = 500;
    private float[] rewards = new float[numEpisodes];
    private float alpha = .1f;
    private float gamma = .9f;

    //epsilon greedy parameters
    private float epsilon = 1f;
    private float min_exploration_rate = .01f;
    private float max_exploration_rate = 1f;
    private float exploration_decay_rate = .01f;

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
        right_accelleration,
        straight_accelleration,
        //straight_reverse
    }
    public static byte numActions = (byte)(Enum.GetValues(typeof(Actions)).Length);

    //Q-table
    private float[,,,,,] qTable = new float[RaycastController.numRayIntervals,
        RaycastController.numRayIntervals,
        RaycastController.numRayIntervals,
        RaycastController.numRayIntervals,
        RaycastController.numRayIntervals,
        numActions];
    
    //Constants used for describe state's elements
    private const byte RAY_SX = 0;
    private const byte RAY_SX_MIDDLE = 1;
    private const byte RAY_MIDDLE = 2;
    private const byte RAY_DX_MIDDLE = 3;
    private const byte RAY_DX = 4;

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
        
        byte[] state = new byte[5];
        byte[] nextstate = new byte[5];
        
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
                while (frame < 20)
                {
                    rigidbody.velocity = velocityLimiter(rigidbody.velocity);
                    yield return null;
                }
                frame = 0;
                nextstate = raycastController.getCurrentState();
                
                reward = reclameReward(state, action, nextstate);
                Debug.Log("Reward: " + reward);
                
                //Bellman equation
                qTable[state[RAY_SX], state[RAY_SX_MIDDLE], state[RAY_MIDDLE], state[RAY_DX_MIDDLE], state[RAY_DX], action] =
                    qTable[state[RAY_SX], state[RAY_SX_MIDDLE], state[RAY_MIDDLE], state[RAY_DX_MIDDLE], state[RAY_DX], action]
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
        byte[] state = new byte[5];
        byte[] nextstate = new byte[5];
        
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
                rigidbody.velocity = velocityLimiter(rigidbody.velocity);
                yield return null;

                /*
                while (frame < 3)
                {
                    rigidbody.velocity = velocityLimiter(rigidbody.velocity);
                    yield return null;
                }
                */
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
        float randValue;

        randValue = (rand.Next(0, 1000)) / 1000f;
        if (randValue > epsilon)
            return findBestAction(state);
        else
            return (byte)rand.Next(0, QLearner.numActions);
    }

    private Vector3 velocityLimiter(Vector3 velocity)
    {
        Vector3 localVelocity = transform.InverseTransformDirection(velocity);
        localVelocity.z = localVelocity.z > 15 ? 15 : localVelocity.z;
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
            if (qTable[state[RAY_SX], state[RAY_SX_MIDDLE], state[RAY_MIDDLE], state[RAY_DX_MIDDLE], state[RAY_DX], i] 
                > bestValue)
            {
                bestValue = qTable[state[RAY_SX], state[RAY_SX_MIDDLE], state[RAY_MIDDLE], state[RAY_DX_MIDDLE], state[RAY_DX], i];
                bestIndexValue = i;
            }
        }
        return bestIndexValue;
    }

    private float maxQValueAction(byte[] state)
    {
        byte index = findBestAction(state);
        return qTable[state[RAY_SX], state[RAY_SX_MIDDLE], state[RAY_MIDDLE], state[RAY_DX_MIDDLE], state[RAY_DX], index];
    }

    private void performAction(CarUserControl carUserControl, Actions action)
    {
        switch (action)
        {
            case Actions.left_accelleration:
                carUserControl.setHorizontal(-0.80f);
                carUserControl.setVertical(0.2f);
                break;
            case Actions.right_accelleration:
                carUserControl.setHorizontal(0.80f);
                carUserControl.setVertical(0.2f);
                break;
            case Actions.straight_accelleration:
                carUserControl.setHorizontal(0);
                carUserControl.setVertical(0.6f);
                break;
                /*
            case Actions.straight_reverse:
                carUserControl.setHorizontal(0);
                carUserControl.setVertical(-1f);
                break;
                */
        }
    }

    private float getScore(int stateValue)
    {
        float score = 0;
        switch (stateValue)
        {
            case 3:
                score = 1f;
                break;
            case 2:
                score = -.3f;
                break;
            case 1:
                score = -0.8f;
                break;
            case 0:
                score = -1.5f;
                break;
        }
        return score;
    }
    
    private float reclameReward(byte[] state, byte action, byte[] nextstate)
    {
        float reward;
        float scoreState = 0;
        //state score
        float leftSensorsScore = getScore(state[RAY_SX]) + getScore(state[RAY_SX_MIDDLE]) + getScore(state[RAY_MIDDLE]);
        float middleSensorsScore = getScore(state[RAY_SX_MIDDLE]) + getScore(state[RAY_MIDDLE]) + getScore(state[RAY_DX_MIDDLE]);
        float rightSensorsScore = getScore(state[RAY_DX]) + getScore(state[RAY_DX_MIDDLE]) + getScore(state[RAY_MIDDLE]);

        float velocity = transform.InverseTransformDirection(rigidbody.velocity).z;

        //action score
        switch (action)
        {
            case 0:
                scoreState = leftSensorsScore;
                break;
            case 1:
                scoreState = rightSensorsScore;
                break;
            case 2:
            case 3:
                scoreState = middleSensorsScore;
                break;
        }
        //reward calculation
        reward = velocity * scoreState;
        /*
        if (velocity < 0 && !isBraking(action) || velocity > 0 && isBraking(action))
            reward = -reward * .1f;
            */
        //ending state: collision
        if (collided)
            reward += -1000;

        return reward;
    }

    private bool isBraking(byte action)
    {
        return action == 3;
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
                                for (byte f = 0; f < numActions; f++)
                                    tw.WriteLine(qTable[a, b, c, d, e, f]);
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
                                for (byte f = 0; f < numActions; f++)
                                    qTable[a, b, c, d, e, f] = float.Parse(lines[index++]);
    }

    public void Update()
    {
        frame++;
    }
}
