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
    private int T = 300;
    private static int numEpisodes = 5000;
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
        bool learn = false;
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
                //epsilon-greedy algorithm for action selection
                action = epsilonGreedy(state);
                
                //action performing into the environment
                yield return step(20, (Actions)action);
                
                //getting nextstate and calculating reward
                nextstate = raycastController.getCurrentState();
                reward = reclameReward(state, action);

                //Bellman equation
                updateQValues(state, action, nextstate, reward);

                //In the end, we update the state and the total reward
                state = nextstate;
                episodeReward += reward;                
            }

            //storing episode reward and updating the epsilon value
            rewards[episode] = episodeReward;
            epsilon = min_exploration_rate + (max_exploration_rate - min_exploration_rate) *
                Mathf.Exp(-exploration_decay_rate * episode);

            Debug.Log("Episodio :" + episode + " Ricompensa: " + episodeReward + "Epsilon :" + epsilon);
        }

        //memorizing the Q-table values and rewards on file
        writeQtableValuesOnFile();
        writeRewardsOnFile();

        //game shutdown
        UnityEditor.EditorApplication.isPlaying = false;
    }

    public IEnumerator playGame()
    {
        byte action;
        byte[] state = new byte[5];
        byte[] nextstate = new byte[5];
        
        for (int episode = 0; episode < numEpisodes; episode++)
        {
            initCarState();
            state = raycastController.getCurrentState();
            while (!collided)
            {
                action = findBestAction(state);
                yield return step(0, (Actions)action);
                nextstate = raycastController.getCurrentState();                
                state = nextstate;
            }
        }
    }

    private void updateQValues(byte[] state, byte action, byte[] nextstate, float reward)
    {
        //Bellman equation
        qTable[state[RAY_SX], state[RAY_SX_MIDDLE], state[RAY_MIDDLE], state[RAY_DX_MIDDLE], state[RAY_DX], action] =
            qTable[state[RAY_SX], state[RAY_SX_MIDDLE], state[RAY_MIDDLE], state[RAY_DX_MIDDLE], state[RAY_DX], action]
            * (1 - alpha) + alpha * (reward + gamma * maxQValueAction(nextstate));
    }

    private IEnumerator step(int numFrames, Actions action)
    {
        performAction(action);
//        rigidbody.velocity = velocityLimiter(rigidbody.velocity);
        while (frame < numFrames)
        {
            yield return null;
        }
        frame = 0;
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
        transform.position = new Vector3(278, 0, -30);//Vector3.zero;
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

    private void performAction(Actions action)
    {
        switch (action)
        {
            case Actions.left_accelleration:
                carUserControl.setHorizontal(-0.75f);
                carUserControl.setVertical(0.20f);
                break;
            case Actions.right_accelleration:
                carUserControl.setHorizontal(0.75f);
                carUserControl.setVertical(0.20f);
                break;
            case Actions.straight_accelleration:
                carUserControl.setHorizontal(0);
                carUserControl.setVertical(0.6f);
                break;
        }
    }

    private float reclameReward(byte[] state, byte action)
    {
        float reward;
        float velocity;

        velocity = transform.InverseTransformDirection(rigidbody.velocity).z;
        //reward calculation
        reward = collided ? -20f : .05f;

        return reward;
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
