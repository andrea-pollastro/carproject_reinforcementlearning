# Car project: learning with RL techniques
I made this project with the colleague Francesco Romeo (https://github.com/francesco1996r) during the university master years as a final part of the robotics exam.
Our goal was to experiment <i>Reinforcement Learning</i> techniques in a simulated environment and for this purpose we've realized a car game whose scope was to let the car learning to drive in a simple environment (drawn only by bareers). 

Our "learning experiments" were divided in 3 parts:
<ol>
  <li><b>Reinforcement Learning</b> using <i>Bellman equation</i></li>
  <li><b>Deep Reinforcement Learning</b> using a <i>Convolutional Neural Network</i></li>
  <li><b>Deep Reinforcement Learning</b> using a <i>Feed-Forward Neural Network</i> with 3 layers and 512 nodes</li>
</ol>
In all the three parts, we used n-rays (starting from the front of the car) to let the car read the environment via Ray Casting (just like simulating lasers/sonars). Any scan of the environment represented the current state on which we could work for the learning phase.
We've made this project using <b>Unity</b> and the <b>ML Agent</b> tool provided by Unity for the ANN realization. 

<a href="https://github.com/andrea-pollastro/carproject_reinforcementlearning/blob/master/ProjectResults.pdf">Here</a> you can find all the results we've got from the project.
