using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

//Our strategy focuses on hunting the opponent down if they are carrying a ball. Otherwise, our robot
//picks up the closest target to our base and retrives it. If our robot is carrying a ball back to the base,
//it does not attack but prioritizes getting the target to the base. If our robot is not carrying anything but
//the opponent is, it hunts down the opponent to shoot them down!

public class MARTRON : CogsAgent
{
    // lastInput is used to keep track of previous values of forwardAxis and rotateAxis
    // feeding values from previous frame into next frame by default makes for smoother movement
    private int[] lastInput = new int[] {1, 0};

    // ------------------BASIC MONOBEHAVIOR FUNCTIONS-------------------
    // Initialize values
    protected override void Start()
    {
        base.Start();
        AssignBasicRewards();
        
    }

    // For actual actions in the environment (e.g. movement, shoot laser)
    // that is done continuously
    protected override void FixedUpdate() {
        base.FixedUpdate();
        
        LaserControl();
        // Movement based on DirToGo and RotateDir
        moveAgent(dirToGo, rotateDir);
    }


    
    // --------------------AGENT FUNCTIONS-------------------------

    // Get relevant information from the environment to effectively learn behavior
    public override void CollectObservations(VectorSensor sensor)
    {
        // Agent velocity in x and z axis 
        var localVelocity = transform.InverseTransformDirection(rBody.velocity);
        sensor.AddObservation(localVelocity.x);
        sensor.AddObservation(localVelocity.z);

        // Time remaning
        sensor.AddObservation(timer.GetComponent<Timer>().GetTimeRemaning());  

        // Agent's current rotation
        var localRotation = transform.rotation;
        sensor.AddObservation(transform.rotation.y);

        // Agent and home base's position
        sensor.AddObservation(this.transform.localPosition);
        sensor.AddObservation(baseLocation.localPosition);

        // for each target in the environment, add: its position, whether it is being carried,
        // and whether it is in a base
        foreach (GameObject target in targets){
            sensor.AddObservation(target.transform.localPosition);
            sensor.AddObservation(target.GetComponent<Target>().GetCarried());
            sensor.AddObservation(target.GetComponent<Target>().GetInBase());
        }
        
        // Whether the agent is frozen
        sensor.AddObservation(IsFrozen());

    }

    // For manual override of controls. This function will use keyboard presses to simulate output from your NN 
    public override void Heuristic(in ActionBuffers actionsOut)
{
        var discreteActionsOut = actionsOut.DiscreteActions;
        // assigning forwardAxis and rotateAxis values the same values that were assigned in the previous frame
        // makes for smoother movement
        discreteActionsOut[0] = lastInput[0];
        discreteActionsOut[1] = lastInput[1];
        // turn laser off by default so agent doesn't get stuck firing laser and being unable to move
        discreteActionsOut[2] = 0;

        // if agent is carrying at least one target, navigate back to base as quickly as possible
        // otherwise, if enemy is carrying a target, hunt down the enemy and shoot laser
        // otherwise, if neither the agent or the enemy is carrying a target, navigate toward the target that is closest to home base
        if (CarriedCount() >= 1) {
         GoToTargetASAP(myBase, discreteActionsOut);   
        }
        else if (GetEnemyCarriedTarget() != null ) {
            HuntEnemy(discreteActionsOut);
        } else if (GetTargetClosestToBase() != null) {
            GoToTargetASAP(GetTargetClosestToBase(), discreteActionsOut);
        }

        // raycasts are used to prevent agent from getting stuck up against a wall
        RaycastHit hit;
        if (Physics.Raycast(this.transform.localPosition, transform.forward, out hit, 0.6f)) {
            if (hit.transform.tag == "Wall") {
                discreteActionsOut[0] = 2;
            }
        }
        if (Physics.Raycast(this.transform.localPosition, -transform.forward, out hit, 0.6f)) {
            if (hit.transform.tag == "Wall") {
                discreteActionsOut[0] = 1;
            }
        }

        // lastInput array keeps track of previous forwardAxis and rotateAxis inputs for smoother movement
        lastInput[0] = discreteActionsOut[0];
        lastInput[1] = discreteActionsOut[1];

    }

        // What to do when an action is received (i.e. when the Brain gives the agent information about possible actions)
        public override void OnActionReceived(ActionBuffers actions){

        int forwardAxis = (int)actions.DiscreteActions[0]; //NN output 0
        int rotateAxis = (int)actions.DiscreteActions[1]; 
        int shootAxis = (int)actions.DiscreteActions[2]; 

        MovePlayer(forwardAxis, rotateAxis, shootAxis);
    }


// ----------------------ONTRIGGER AND ONCOLLISION FUNCTIONS------------------------
    // Called when object collides with or trigger (similar to collide but without physics) other objects
    protected override void OnTriggerEnter(Collider collision)
    {
        

        
        if (collision.gameObject.CompareTag("HomeBase") && collision.gameObject.GetComponent<HomeBase>().team == GetTeam())
        {
            //Add rewards here
        }
        base.OnTriggerEnter(collision);
    }

    protected override void OnCollisionEnter(Collision collision) 
    {
        

        //target is not in my base and is not being carried and I am not frozen
        if (collision.gameObject.CompareTag("Target") && collision.gameObject.GetComponent<Target>().GetInBase() != GetTeam() && collision.gameObject.GetComponent<Target>().GetCarried() == 0 && !IsFrozen())
        {
            
        }

        if (collision.gameObject.CompareTag("Wall"))
        {
            
        }
        base.OnCollisionEnter(collision);
    }



    //  --------------------------HELPERS---------------------------- 
     private void AssignBasicRewards() {
        rewardDict = new Dictionary<string, float>();

        rewardDict.Add("frozen", 0f);
        rewardDict.Add("shooting-laser", 0f);
        rewardDict.Add("hit-enemy", 0f);
        rewardDict.Add("dropped-one-target", 0f);
        rewardDict.Add("dropped-targets", 0f);
    }

    // returns number of targets being carried by agent
    private int CarriedCount() {
        int count = 0;
        foreach (GameObject target in targets){
            if (target.GetComponent<Target>().GetCarried() != 0 &&
                Mathf.Abs(target.transform.localPosition.x - transform.localPosition.x) < 1 &&
                Mathf.Abs(target.transform.localPosition.z - transform.localPosition.z) < 1) {
                count++;
            }
        }
        return count;
    }
    
    private void MovePlayer(int forwardAxis, int rotateAxis, int shootAxis)
    {

        Vector3 forward = transform.forward;
        Vector3 backward = -transform.forward;
        Vector3 right = transform.up;
        Vector3 left = -transform.up;

         //fowardAxis: 
            // 0 -> do nothing
            // 1 -> go forward
            // 2 -> go backward
        if (forwardAxis == 0){
            //do nothing
        }
        else if (forwardAxis == 1){
            dirToGo = forward;
        }
        else if (forwardAxis == 2){
            dirToGo = backward;
            
        }

        //rotateAxis: 
            // 0 -> do nothing
            // 1 -> go right
            // 2 -> go left
        if (rotateAxis == 0){
            //do nothing
        } else if (rotateAxis == 1) {
            rotateDir = right;
        } else if (rotateAxis == 2) {
            rotateDir = left;
        }

        //shoot
        if (shootAxis == 1){
            SetLaser(true);
        }
        else {
            SetLaser(false);
        }
        
    }

    // this function is called if the enemy is carrying a target according to GetEnemyCarriedTarget()
    // navigate toward enemy by moving forward and fire the laser if the enemy is within laser range
    private void HuntEnemy(ActionSegment<int> discreteActions) {
        if (Vector3.Distance(GetEnemyCarriedTarget().transform.localPosition, transform.localPosition) > 20) {
            GoToTarget(GetEnemyCarriedTarget(), discreteActions);
        } else {
            GoToTarget(GetEnemyCarriedTarget(), discreteActions);
            discreteActions[2] = 1;
        }
    }

    // returns target carried by enemy (if any)
    private GameObject GetEnemyCarriedTarget() {
        GameObject enemyCarriedTarget = null;
        foreach (GameObject target in targets){
            if (target.GetComponent<Target>().GetCarried() != 0 &&
                Mathf.Abs(target.transform.localPosition.x - transform.localPosition.x) >= 1 &&
                Mathf.Abs(target.transform.localPosition.z - transform.localPosition.z) >= 1) {
                enemyCarriedTarget = target;
                break;
            }
        }
        return enemyCarriedTarget;
    }

    // go to target by moving forward
    private void GoToTarget(GameObject target, ActionSegment<int> discreteActions) {
        float rotation = GetYAngle(target);
        TurnAndGo(rotation, discreteActions);
    }

    // go to to target by moving forward or backward (whichever will get the agent there faster)
    private void GoToTargetASAP(GameObject target, ActionSegment<int> discreteActions) {
        float rotation = GetYAngle(target);
        // check if target is in front of or behind agent
        if (Mathf.Abs(rotation) <= 90) {
            TurnAndGo(rotation, discreteActions);
        } else {
            TurnAndGoBackward(rotation, discreteActions);
        }
    }

    // Identifies target that is not yet captured that is closest to home base
    private GameObject GetTargetClosestToBase() {
        float distance = 200;
        GameObject targetCloseToBase = null;
        foreach (var target in targets)
        {
            // distance between target and home base
            float currentDistance = Vector3.Distance(target.transform.localPosition, myBase.transform.localPosition);
            if (currentDistance < distance && target.GetComponent<Target>().GetCarried() == 0 && target.GetComponent<Target>().GetInBase() != team){
                distance = currentDistance;
                targetCloseToBase = target;
            }
        }
        return targetCloseToBase;
    }

    // Rotate and go in specified direction by moving forward
    private void TurnAndGo(float rotation, ActionSegment<int> discreteActions){

        if(rotation < -5f){
            //rotateDir = transform.up;
            discreteActions[1] = 1;
        }
        else if (rotation > 5f){
            //rotateDir = -transform.up;
            discreteActions[1] = 2;
        }
        else {
            //dirToGo = transform.forward;
            discreteActions[0] = 1;
        }
    }

    // Rotate and go in specified direction by moving backward
    private void TurnAndGoBackward(float rotation, ActionSegment<int> discreteActions){

        if(rotation > -175f && rotation < -90f){
            //rotateDir = -transform.up;
            discreteActions[1] = 2;
        }
        else if (rotation < 175f && rotation > 90f){
            //rotateDir = transform.up;
            discreteActions[1] = 1;
        }
        else {
            //dirToGo = -transform.forward;
            discreteActions[0] = 2;
        }
    }

    // get angle between agent and targe for navigation purposes
    private float GetYAngle(GameObject target) {
        
       Vector3 targetDir = target.transform.position - transform.position;
       Vector3 forward = transform.forward;

      float angle = Vector3.SignedAngle(targetDir, forward, Vector3.up);
      return angle; 
        
    }
}
