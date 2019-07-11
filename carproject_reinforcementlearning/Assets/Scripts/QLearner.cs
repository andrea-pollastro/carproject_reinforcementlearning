using System.Threading.Tasks;
using System.Threading;
using UnityEngine;
using System.Collections;
using System.Linq; //for array equals
using UnityStandardAssets.Vehicles.Car;


public class QLearner : MonoBehaviour
{
    //hyperparameters
    private static int episodes = 50000;
    private float[] rewards = new float[episodes];
    private int maxSteps = 10000;
    private float alpha = .4f;
    private float gamma = .99f;

    //epsilon greedy parameters
    private float epsilon = 1f;
    private float min_exploration_rate = .01f;
    private float max_exploration_rate = 1f;
    private float exploration_decay_rate = .01f;

    //state definition parameters
    private int nDimensionState = 6; /*Please, if change it, update the variables for simple coding and the dimension of state array*/

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

    //Variables for simple coding
    private static int red = 0;
    private static int orange = 1;
    private static int yellow = 2;
    private static int green = 3;

    private static int sensor_sx = 0;
    private static int sensor_semi_sx = 1;
    private static int sensor_straight = 2;
    private static int sensor_semi_dx = 3;
    private static int sensor_dx = 4;
    private static int velocityX = 5;
    private static int velocityZ = 6;

    private static int left = 0;
    private static int right = 1;
    private static int straight = 2;

    private static int accelleration = 0;
    private static int inertial = 1;
    private static int reverse = 2;

    private static int strong_reverse = 0;
    private static int weak_reverse = 1;
    private static int weak_speed = 2;
    private static int medium_speed = 3;
    private static int strong_speed = 4;

    private static int strong_left = 0;
    private static int weak_left = 1;
    private static int weak_right = 2;
    private static int strong_right = 3;
    private static int very_strong_right = 4;


    //Controller needed to reset the condition to starting condition
    private CarUserControl carUserControl;
    private Rigidbody rigidbody;
    private RaycastController raycastController;

    //Q-table
    private float[,,,,,,,] qTable = new float[5,5,5,5,5,5,5,9];
    private float[,,,,] rewardTable = new float[5,5,5,5,5];

    //needed for choose random number
    private readonly System.Random rand = new System.Random();

    //collision detected flag
    bool collided = false;

    //Initial GameObject state
    private Vector3 initPosition;
    private Quaternion initOrientation;

    //needed for performing action
    //byte numFrame = 0;
    int step = 0;
    int expectedIndex;

    // Start is called before the first frame update
    void Start()
    {
        //getting components for drive the car
        carUserControl = GetComponent<CarUserControl>();
        rigidbody = GetComponent<Rigidbody>();
        raycastController = GetComponent<RaycastController>();

        //saving info for init the car position
        initPosition = transform.position;
        initOrientation = transform.rotation;

        //Construction of rewards table
        createRewardTable();

        //Executing the routine for Learning
        StartCoroutine(executeLearning());
    }

    public IEnumerator executeLearning()
    {
        float currentReward;
        float myLittleReward;
        float randValue;
        byte action;
    
        /*  State mapping:
         *  0: collision
         *  1: red
         *  2: orange
         *  3: yellow
         *  4: green
         */
        byte[] state = new byte[7];
        byte[] nextState = new byte[7];
        //float old_velocity;
        //float velocity;
        //Mathf.Abs((rigidbody.velocity.x + rigidbody.velocity.y + rigidbody.velocity.z));

        for (int episode = 0; episode < episodes; episode++)
        {
            //reinit state
            transform.position = initPosition;
            transform.rotation = initOrientation;
            rigidbody.velocity = Vector3.zero;
            collided = false;

            //reinit the cycle variables
            step = 0;
            currentReward = 0;

            while( step < maxSteps && !collided )
            {
                //stops this method and lets the game engine to elaborate the current frame
                yield return null;

                //First of all, we get the state of the car
                state = raycastController.getCurrentState();
                //velocity = detectVelocity();
                //Debug.Log("Stato iniziale acquisito");

                //We choice a rand value for execute an explotation or an exploration
                randValue = (rand.Next(0, 1000)) / 1000f;

                //if that rand value is greater that an fixed epsilon, we perform ad exploitation
                if (randValue > epsilon)
                {
                    //With other word, we choise the action that maximize the value of the current state
                    action = maximize(state);
                    Debug.Log("Questa volta si massimizza!");
                }
                //Else, if the rand value is lower that epsilon, we perform an exploration
                else
                {
                    //Exploration consists in a choise of a random action: we implement it through an integer
                    action = (byte)rand.Next(0, 9);
                    Debug.Log("Questa volta si esplora!");

                }

                //Once the action is chosen, we can perform that action
                performAction(carUserControl, (Actions)action);

                //Debug.Log("Ho effettuato l'azione " + (Actions)action + " "+ randValue);

                //Then we wait until the action is totally performed, that is, the state is changed

                //For detect this changes, we use a dedicate variables that hold the current state

                //old_velocity = normalize(velocity);
                state.CopyTo(nextState, 0);

                //For be more precisely, we want that a specific index change its state, so we calculate it
                expectedIndex = expectedState(state, action);
                
                int securutyCounter = 0; /* this is for debug */
                //Debug.Log("Lo stato che mi attendo che cambi è " + expectedIndex);
                //With this cycle, we check if the change has been happend
                while ((expectedIndex != -1 && state[expectedIndex] == nextState[expectedIndex]))
                {
                    yield return null;
                    securutyCounter++;
                    nextState = raycastController.getCurrentState();
                    performAction(carUserControl, (Actions)action);
                    if (collided)
                        break;
                    if (securutyCounter > 1000)
                        break;
                    //Debug.Log(securutyCounter);
                }

                //Debug.Log("Lo stato è cambiato nel bene o nel male (" + securutyCounter + ")");

                //If we are able to execute this code, then the action would be performed, and the state is changed

                //So we reclame the reward for the state we have reached
                /*if (alertSx(state))
                    Debug.Log("accort a sinistra");
                if (alertDx(state))
                    Debug.Log("accort a destra");
                if (alertFront(state))
                    Debug.Log("accort annanz");*/
                myLittleReward = reclameReward(nextState);
                //Debug.Log("La reward ottenuta per " + (Actions)action + " è :" + myLittleReward);
                //Debug.Log(state[0] + " " + state[1] + " " + state[2] + " " + state[3] + " " + state[4] + " " + 0 + " " + action);

                //And we update the Qtable using the Bellman equation
                qTable[state[sensor_sx], state[sensor_semi_sx], state[sensor_straight], state[sensor_semi_dx], state[sensor_dx], state[velocityX],state[velocityZ], action] =
                    qTable[state[sensor_sx], state[sensor_semi_sx], state[sensor_straight], state[sensor_semi_dx], state[sensor_dx], state[velocityX], state[velocityZ], action] 
                    * (1 - alpha) + alpha * (myLittleReward + gamma * maximize(nextState));
                
                //Debug.Log("Nuovo valore della Qtable: "+ qTable[state[0], state[1], state[2], state[3], state[4], state[5], state[6], action]);
                //In the end, we update the state and the current total reward
                state = nextState;
                currentReward += myLittleReward;
                
                //Debug.Log(state[0] + " " + state[1] + " " + state[2] + " " + state[3] + " " + state[4] + " " );
            }

            //At end of every episode, we save the actual reward and update the epsilon (for exploring)
            rewards[episode] = currentReward;
            epsilon = min_exploration_rate + (max_exploration_rate - min_exploration_rate) *
                Mathf.Exp(-exploration_decay_rate * episode);

            Debug.Log("Episodio :" + episode + " Ricompensa: " + currentReward + "Epsilon :" + epsilon);
        }
    }
    
    private byte maximize(byte[] state)
    {
        byte bestIndexValue = 0;
        float bestValue = 0;

        for(byte i = 0; i < 9; i++)
        {
            //Debug.Log(state[0] + " " + state[1] + " " + state[2] + " " + state[3] + " " + state[4] + " " + 0 + " " +i);
            //Debug.Log(normalize(velocity));
            if (qTable[state[sensor_sx], state[sensor_semi_sx], state[sensor_straight], state[sensor_semi_dx], state[sensor_dx], state[velocityX], state[velocityZ], i] > bestValue)
            {                
                bestValue = qTable[state[sensor_sx], state[sensor_semi_sx], state[sensor_straight], state[sensor_semi_dx], state[sensor_dx], state[velocityX], state[velocityZ], i];
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
                carUserControl.setVertical(0.2f);
                break;
            case Actions.left_only:
                carUserControl.setHorizontal(-0.85f);
                carUserControl.setVertical(0);
                break;
            case Actions.left_reverse:
                carUserControl.setHorizontal(-0.5f);
                carUserControl.setVertical(-0.3f);
                //carUserControl.setVertical(0);
                break;
            case Actions.right_accelleration:
                carUserControl.setHorizontal(0.6f);
                carUserControl.setVertical(0.2f);
                break;
            case Actions.right_only:
                carUserControl.setHorizontal(0.85f);
                carUserControl.setVertical(0);
                break;
            case Actions.right_reverse:
                carUserControl.setHorizontal(0.4f);
                carUserControl.setVertical(-0.3f);
                //carUserControl.setVertical(0);
                break;
            case Actions.straight_accelleration:
                carUserControl.setHorizontal(0);
                carUserControl.setVertical(0.7f);
                break;
            case Actions.straight_only:
                carUserControl.setHorizontal(0);
                carUserControl.setVertical(0);
                break;
            case Actions.straight_reverse:
                carUserControl.setHorizontal(0);
                carUserControl.setVertical(-0.7f);
                //carUserControl.setVertical(0);
                break;
        }
    }

    private void createRewardTable()
    {
        /*for (byte a = 0; a < 5; a++)
            for (byte b = 0; b < 5; b++)
                for (byte c = 0; c < 5; c++)
                    for (byte d = 0; d < 5; d++)
                        for (byte e = 0; e < 5; e++)
                            rewardTable[a, b, c, d, e] = getScore(a) + getScore(b) +
                                getScore(c) + getScore(d) + getScore(e);*/
    }

    private float getScoreMovement(byte a)
    {
        float score = 0;
        switch (a)
        {
            case 4:
                score = 0.2f;
                break;
            case 3:
                score = 0.1f;
                break;
            case 2:
                score = -0.1f;
                break;
            case 1:
                score = -0.2f;
                break;
            case 0:
                score = -1f;
                break;
        }
        return score;
    }

    private float getScoreVelocity(byte a)
    {
        float score = 0;
        switch (a)
        {
            case 4:
                score = 0.4f;
                break;
            case 3:
                score = 0.5f;
                break;
            case 2:
                score = 0.2f;
                break;
            case 1:
                score = 0.1f;
                break;
            case 0:
                score = -0.5f;
                break;
        }
        return score;
    }

    private int getMovement(byte action)
    {
        return action / 3;
    }

    private int getAcceleration(byte action)
    {
        return action % 3;
    }

    private float reclameReward(byte[] state)
    {
        //return rewardTable[state[0], state[1], state[2], state[3], state[4]] * Mathf.Abs((rigidbody.velocity.x+ rigidbody.velocity.y+ rigidbody.velocity.z));
        float rewardMovement = getScoreMovement(state[sensor_sx]) + getScoreMovement(state[sensor_semi_sx]) + getScoreMovement(state[sensor_straight]) + getScoreMovement(state[sensor_semi_dx]) + getScoreMovement(state[sensor_dx]);
        float rewardVelocity = getScoreVelocity(state[velocityZ]);
        float bonus = 0;
        float totalReward;

        //Se ho un muro a sinistra e la mia velocità va verso sinistra
        if (alertSx(state) && state[velocityX] <= weak_left)
            bonus += -1.5f;

        //Se ho un muro a destra e la mia velocità va verso destra
        if (alertDx(state) && state[velocityX] >= weak_right)
            bonus += -1.5f;

        //Se ho una velocità sostenuta ed un muro di fronte
        if (alertFront(state) && state[velocityZ] >= medium_speed)
            bonus += 2 - state[velocityZ]; /* -1 if orange, -2 if red*/

        //Se ho davanti la strada libera, e vado a retromarcia
        if (!alertSx(state) && ! alertDx(state) && !alertFront(state) && state[velocityZ] < weak_speed)
            bonus += -1.5f;

        return rewardMovement + rewardVelocity + bonus;
    }

    private int expectedState(byte[] state, byte action)
    {
        if (getAcceleration(action) == inertial && state[velocityX] == 1 && state[velocityZ] == 1)
        {
            return -1;
        }
        else if (getAcceleration(action) == accelleration && getMovement(action) == left)
        {
            return sensor_semi_sx;// temp[sensor_semi_sx] = (byte) (state[sensor_semi_sx] - 1);
        }
        else if (getAcceleration(action) == accelleration && getMovement(action) == right)
        {
            return sensor_semi_dx;//temp[sensor_semi_dx] = (byte)(state[sensor_semi_dx] - 1);
        }
        else if (getAcceleration(action) == accelleration)
        {
            return sensor_straight;//temp[sensor_straight] = (byte)(state[sensor_straight] - 1);
        }
        else if (getAcceleration(action) == reverse && getMovement(action) == left)
        {
            return sensor_semi_dx;//temp[sensor_straight] = (byte)(state[sensor_straight] - 1);
        }
        else if (getAcceleration(action) == reverse && getMovement(action) == right)
        {
            return sensor_semi_sx;//temp[sensor_straight] = (byte)(state[sensor_straight] - 1);
        }
        else if (getAcceleration(action) == reverse && (state[velocityX]<=1 || state[velocityZ]<=1) )
        {
            return -1;
        }
        else if (getAcceleration(action) == reverse)
        {
            return state[velocityX] > state[velocityZ] ? velocityX : velocityZ;
        }


        return -1;
    }

    private void OnCollisionEnter(Collision collision)
    {
        collided = true;
    }

    private bool alertSx(byte[] state)
    {
        return state[sensor_sx] < yellow || state[sensor_semi_sx] == red;
    }

    private bool alertDx(byte[] state)
    {
        return state[sensor_dx] < yellow || state[sensor_semi_dx] == red;
    }

    private bool alertFront(byte[] state)
    {
        return state[sensor_straight] <= orange;
    }
}

