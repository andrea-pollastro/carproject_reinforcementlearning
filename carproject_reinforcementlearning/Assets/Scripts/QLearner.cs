using System.Threading.Tasks;
using System.Threading;
using UnityEngine;
using System.Collections;
using UnityStandardAssets.Vehicles.Car;

public class QLearner : MonoBehaviour
{
    //Actions
    enum Actions
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
    //Controller needed to reset the condition to starting condition
    private CarUserControl carUserControl;
    private Rigidbody rigidbody;
    private RaycastController raycastController;

    //Q-table
    private float[,,,,,] qTable = new float[5,5,5,5,5,9];
    private float[,,,,] rewardTable = new float[5,5,5,5,5];

    //hyperparameters
    private int episodes = 1000;
    private float[] rewards = new float[1000];
    private int maxSteps = 10000;
    private float alpha = .5f;
    private float gamma = .99f;
    //epsilon greedy parameters
    private float epsilon = 1f;
    private float min_exploration_rate = .01f;
    private float max_exploration_rate = 1f;
    private float exploration_decay_rate = .01f;

    //needed for random
    private readonly System.Random rand = new System.Random();
    //collision detected flag
    bool collided = false;
    //Initial GameObject state
    private Vector3 initPosition;
    private Quaternion initOrientation;

    //needed for performing action
    byte numFrame = 0;

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
        createRewardTable();
        StartCoroutine(executeLearning());
    }

    private void OnCollisionEnter(Collision collision)
    {
        collided = true;
    }

    public IEnumerator executeLearning()
    {
        float currentReward;
        float randValue;
        byte action;
        /*  State mapping:
         *  0: red
         *  1: orange
         *  2: yellow
         *  3: green
         *  4: collision
         */
        byte[] state = new byte[5];
        byte[] nextState = new byte[5];

        for (int episode = 0; episode < episodes; episode++)
        {
            //reinit state
            transform.position = initPosition;
            transform.rotation = initOrientation;
            rigidbody.velocity = Vector3.zero;
            currentReward = 0;
            collided = false;

            for (int step = 0; step < maxSteps && !collided; step++)
            {
                //stops this method and lets the game engine to elaborate the current frame
                yield return null;
                if (numFrame != 10)
                    continue;
                numFrame = 0;
                state = raycastController.getCurrentState(state);
                randValue = (float)((rand.Next(0, 1000)) / 1000);
                if (randValue > epsilon)
                {
                    //exploitation
                    action = max(state);
                }
                else
                {
                    //exploration
                    action = (byte)rand.Next(0, 9);
                }
                //perform action
                performAction(carUserControl, (Actions)action);
                nextState = raycastController.getCurrentState(state);
                if (collided)
                    nextState = detectCollisionSide(nextState);
                //Bellman equation
                qTable[state[0], state[1], state[2], state[3], state[4], action] =
                    qTable[state[0], state[1], state[2], state[3], state[4], action] * (1 - alpha) +
                    alpha * (getReward(state) + gamma * max(nextState));
                //updating state and total reward
                nextState = state;
                currentReward += getReward(state);
            }
            rewards[episode] = currentReward;
            epsilon = min_exploration_rate + (max_exploration_rate - min_exploration_rate) *
                Mathf.Exp(-exploration_decay_rate * episode);
        }
    }

    public void Update()
    {
        numFrame++;
    }

    private byte[] detectCollisionSide(byte[] state)
    {
        for (byte i = 0; i < state.Length; i++)
            state[i] = state[i] == 0 ? state[i] = 4 : state[i];
        return state;
    }

    private byte max(byte[] state)
    {
        byte bestIndexValue = 0;
        float bestValue = 0;
        for(byte i = 0; i < 9; i++)
        {
            if(qTable[state[0], state[1], state[2], state[3], state[4], i] > bestValue)
            {
                bestValue = qTable[state[0], state[1], state[2], state[3], state[4], i];
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
                carUserControl.setHorizontal(-1);
                carUserControl.setVertical(1);
                break;
            case Actions.left_only:
                carUserControl.setHorizontal(-1);
                carUserControl.setVertical(0);
                break;
            case Actions.left_reverse:
                carUserControl.setHorizontal(-1);
                carUserControl.setVertical(-1);
                break;
            case Actions.right_accelleration:
                carUserControl.setHorizontal(1);
                carUserControl.setVertical(1);
                break;
            case Actions.right_only:
                carUserControl.setHorizontal(1);
                carUserControl.setVertical(0);
                break;
            case Actions.right_reverse:
                carUserControl.setHorizontal(1);
                carUserControl.setVertical(-1);
                break;
            case Actions.straight_accelleration:
                carUserControl.setHorizontal(0);
                carUserControl.setVertical(1);
                break;
            case Actions.straight_only:
                carUserControl.setHorizontal(0);
                carUserControl.setVertical(0);
                break;
            case Actions.straight_reverse:
                carUserControl.setHorizontal(0);
                carUserControl.setVertical(-1);
                break;
        }
    }

    private void createRewardTable()
    {
        for (byte a = 0; a < 5; a++)
            for (byte b = 0; b < 5; b++)
                for (byte c = 0; c < 5; c++)
                    for (byte d = 0; d < 5; d++)
                        for (byte e = 0; e < 5; e++)
                            rewardTable[a, b, c, d, e] = getScore(a) + getScore(b) +
                                getScore(c) + getScore(d) + getScore(e);
    }

    private float getScore(byte a)
    {
        float score = 0;
        switch (a)
        {
            case 4:
                score = -10;
                break;
            case 3:
                score = 0.3f;
                break;
            case 2:
                score = 0;
                break;
            case 1:
                score = -.4f;
                break;
            case 0:
                score = -.8f;
                break;
        }
        return score;
    }

    private float getReward(byte[] state)
    {
        return rewardTable[state[0], state[1], state[2], state[3], state[4]] * Mathf.Abs((rigidbody.velocity.x+ rigidbody.velocity.y+ rigidbody.velocity.z));
    }
}
