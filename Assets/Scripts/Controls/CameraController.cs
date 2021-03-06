﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public class CameraController : MonoBehaviour
{
    //reference to the player to look at
    public Transform player;
    //reference to what the player is locking on to
    Transform target;
    public Transform targettedAppendage;
    public Appendage targettedAppendageScript;
    private new Rigidbody rigidbody;
    public CameraSettings cameraSettings;
    public ParticleSystem particle;
    float x = 0.0f;
    float y = 0.0f;
    float deadzone = 0.5f;
    //Whether or not the player wants to and has targeted an enemy
    bool tryLockOn = false;
    bool lockedOn = false;
    bool targettingAppendages = false;
    // Use this for initialization
    void Start()
    {
        Vector3 angles = transform.eulerAngles;
        x = angles.y;
        y = angles.x;

        rigidbody = GetComponent<Rigidbody>();

        // Make the rigid body not change rotation
        if (rigidbody != null)
        {
            rigidbody.freezeRotation = true;
        }
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player").transform;
        }
        Camera.main.fieldOfView = cameraSettings.FOV;
    }

    void LateUpdate()
    {
        //do we want to be locked on?
        tryLockOn = lockedOn ^ Input.GetButtonDown("XboxRightStickClick");
        //don't want to be locked on
        if (!tryLockOn)
        {
            //stop being locked on
            lockedOn = false;
            //clear the target for future use
            target = null;
            targettedAppendage = null;
            targettingAppendages = false;
        }
        //not yet locked on but we want to be
        else if (tryLockOn && !lockedOn)
        {
            //see if there are any valid targets
            //and need to find the 'best' target
            FindBestTarget();
            if (target != null)
            {
                lockedOn = true;
            }
        }
        if (lockedOn && target != null)
        {
            LockedOn();
            if (!particle.isPlaying)
            {
                particle.Play();
            }
        }
        else
        {
            particle.Stop();
            Unlocked();

        }

    }

    void LockedOn()
    {
        if (Input.GetButtonDown("XboxLB"))
        {
            ChangeTarget(true);
        }
        else if (Input.GetButtonDown("XboxRB"))
        {
            ChangeTarget(false);
        }

        Quaternion rotation = transform.rotation;
        Vector3 negDistance = new Vector3(0.0f, 0.0f, -cameraSettings.distance);
        Vector3 position = transform.rotation * negDistance + player.position + (transform.rotation * cameraSettings.offset);
        Vector3 direction;
        if (targettedAppendage!=null)
        {
            direction = targettedAppendage.position - position;
            particle.transform.position = targettedAppendage.position;
        }
        else
        {
            direction = target.position - position;
        }
        //slerp to make pretty rotating effect
        rotation = Quaternion.Slerp(rotation, Quaternion.LookRotation(direction), Time.deltaTime * cameraSettings.lockOnSpeed);
        transform.position = position;
        transform.rotation = rotation;
        
        if (!targettingAppendages)
        {
            StartCoroutine(TargetAppendages());
        }
    }

    IEnumerator TargetAppendages()
    {
        //find the default appendage (upper chest)
        targettedAppendage = FindRootBone();

        while (lockedOn && target != null)
        {
            targettingAppendages = true;
            float inputx = Input.GetAxis("XboxRightHorizontal");
            float inputy = Input.GetAxis("XboxRightVertical");
            //if theres any big boi input
            if (inputx > deadzone || inputx < -deadzone || inputy > deadzone || inputy < -deadzone)
            {

                //may need to convert this to be respective of the rotation of the target...
                Vector3 inputDir = new Vector3(inputx, inputy, 0);
                //convert inputDir to accomodate player rotation
                
                Quaternion inputRot = Quaternion.AngleAxis(transform.rotation.eulerAngles.y,Vector3.up);
                /*
                 * this is not conforming
                 * basically the x input should be rotated around the Y axis (i.e. become an x-z direction)
                 * whereas the y input should simply be as-is
                 * 
                 * 
                 * 
                 * 
                 * 
                 */
                inputDir = inputRot * inputDir;
                Transform newTarget = FindBestAppendage(inputDir);
                yield return null;
                if (targettedAppendage != newTarget)
                {
                    targettedAppendage = newTarget;

                    yield return new WaitForSeconds(0.5f);

                }
            }
            yield return null;
        }
        targettingAppendages = false;
        yield return null;
    }
    Transform FindRootBone()
    {
        try
        {
            BaseEntity targetInfo = target.GetComponent<BaseEntity>();
            List<Appendage> appendages = targetInfo.appendages;
            
            if (appendages.Count > 0)
            {
                targettedAppendageScript = appendages.Find(x => x.baseAppendageData.appendageType == AppendageData.AppendageType.TorsoU);
                
                return targettedAppendageScript.collider.gameObject.transform;
            }
            else
            {
                return null;
            }
        }
        catch
        {
            Debug.LogWarning("This target is missing appendage information");
            return null;
        }

    }
    Transform FindBestAppendage(Vector3 inputDirection)
    {
        try
        {
            List<Appendage> appendages = target.GetComponent<BaseEntity>().appendages;
            Appendage best = targettedAppendageScript;
            float boundsSize = Mathf.Max(target.gameObject.GetComponentInChildren<Renderer>().bounds.max.x, target.gameObject.GetComponentInChildren<Renderer>().bounds.max.y, target.gameObject.GetComponentInChildren<Renderer>().bounds.max.z);
            //max allowed angle + the max possible modified distance (45 + 45)
            float bestVal = 45 + boundsSize*45;
            float angle;
            float relativeDistance;
            float value;
            if (appendages.Count > 1)
            {
                for (int i = 0; i < appendages.Count; i++)
                {
                    if (targettedAppendageScript == appendages[i])
                    {
                        continue;
                    }
                    angle = Vector3.Angle(inputDirection, appendages[i].collider.transform.position - targettedAppendage.position);
                    
                    relativeDistance = (targettedAppendage.position - appendages[i].collider.transform.position).magnitude /boundsSize;
                    //if they're basically the same part don't allow for change
                    if (relativeDistance < 0.01)
                    {
                        continue;
                    }
                    //distance / boundsSize should be in [0,1], so we want to multiply this so that 1 is roughly the max angle
                    value = angle + 90 *  relativeDistance;
                    if (angle < 45 && value < bestVal)
                    {
                        best = appendages[i];
                        bestVal = value;
                    }

                }

                targettedAppendageScript = best;
                return best.collider.gameObject.transform;

            }
            else
            {
                return null;
            }
        }
        catch
        {
            Debug.LogWarning("not implemented");
            return null;
        }
    }

    private void OnDrawGizmos()
    {

        float inputx = Input.GetAxis("XboxRightHorizontal");
        float inputy = Input.GetAxis("XboxRightVertical");
        //if theres any big boi input
        if (inputx > deadzone || inputx < -deadzone || inputy > deadzone || inputy < -deadzone)
        {

            //may need to convert this to be respective of the rotation of the target...
            Vector3 inputDir = new Vector3(inputx, inputy, 0);
            Quaternion inputRot = Quaternion.AngleAxis(transform.rotation.eulerAngles.y, Vector3.up);

            inputDir = inputRot * inputDir;
            if(targettedAppendage!=null)
            Gizmos.DrawLine(targettedAppendage.position, targettedAppendage.position + inputDir);

        }
    }
    //private void OnDrawGizmos()
    //{
    //    if(targettedAppendage!=null)
    //        Gizmos.DrawCube(targettedAppendage.position, Vector3.one / 2f);
    //}


    void Unlocked()
    {

        x += Input.GetAxis("XboxRightHorizontal") * cameraSettings.xSpeed * cameraSettings.distance;
        if (!cameraSettings.invertY)
            y -= Input.GetAxis("XboxRightVertical") * cameraSettings.ySpeed;
        else
            y += Input.GetAxis("XboxRightVertical") * cameraSettings.ySpeed;
        y = ClampAngle(y, cameraSettings.yMinLimit, cameraSettings.yMaxLimit);

        Quaternion rotation = Quaternion.Euler(y, x, 0);
        Vector3 negDistance = new Vector3(0.0f, 0.0f, -cameraSettings.distance);
        //not sure about the offset term
        Vector3 position = rotation * negDistance + player.position + (rotation * cameraSettings.offset);
        transform.position = position;
        transform.rotation = rotation;
    }

    //Finds all the enemies seen by the main camera
    void FindValidTargets(ref List<GameObject> validTargets)
    {
        //may have a script which can manage this with spawning/despawning, etc.
        //HAVE TO GIVE EVERY ENEMY A TAG THAT ACKNOWLEDGES THEM AS AN ENEMY
        GameObject[] enemiesInScene = GameObject.FindGameObjectsWithTag("Enemy");
        if (enemiesInScene.Length >= 1)
        {
            //check if it is a valid target
            foreach (GameObject enemy in enemiesInScene)
            {
                if (LiesInCamera(enemy.transform.position))
                {
                    validTargets.Add(enemy);
                }
            }
        }
    }
    //Given a Vector3, tells if it appears in camera
    bool LiesInCamera(Vector3 position)
    {
        Vector3 pt = Camera.main.WorldToViewportPoint(position);
        if (pt.x > 1 || pt.x < 0 || pt.y > 1 || pt.y < 0 || pt.z < 0)
        {
            return false;

        }
        else
        {
            RaycastHit hit;
            //layer 11 is entities
            LayerMask mask = ~11;
           
            if (!Physics.Raycast(Camera.main.transform.position, position - Camera.main.transform.position, out hit, Vector3.Magnitude(position - Camera.main.transform.position),mask))
            {
                return true;
            }
            else if (hit.collider.gameObject.layer == 11)
            {
                return true;
            }
            else
            {
                Debug.DrawRay(Camera.main.transform.position,hit.point,Color.magenta,2f);
                Debug.Log(hit.collider.gameObject.ToString());
                
            }
        }
        return false;
    }
    bool FindBestTarget()
    {
        List<GameObject> validTargets = new List<GameObject>();
        FindValidTargets(ref validTargets);
        //if theres only one valid target - we lock on to that one
        if (validTargets.Count == 1)
        {
            target = validTargets[0].transform;
            return true;
        }
        //if there are multiple targets pick the best
        else if (validTargets.Count > 1)
        {
            target = validTargets[0].transform;
            foreach (GameObject obj in validTargets)
                target = BestOutOfTwoValidTargets(target, obj.transform);
            return true;
        }
        //no targets were found
        target = null;
        return false;
    }

    Transform BestOutOfTwoValidTargets(Transform target1, Transform target2)
    {
        float angle1 = Vector3.Angle(target1.position - transform.position, transform.forward);
        float angle2 = Vector3.Angle(target2.position - transform.position, transform.forward);
        float dist1 = Vector3.Magnitude(target1.position - transform.position);
        float dist2 = Vector3.Magnitude(target2.position - transform.position);
        float distanceScale = 0.1f;
        //want the smallest angle and smallest distance
        float invLikelihood1 = angle1 * angle1 + (dist1 * distanceScale);
        float invLikelihood2 = angle2 * angle2 + (dist2 * distanceScale);
        if (invLikelihood1 <= invLikelihood2)
            return target1;
        else
        {
            return target2;
        }
    }

    //given a current target and a direction to go (left/right)
    void ChangeTarget(bool leftTrightF)
    {
        List<GameObject> validTargets = new List<GameObject>();
        FindValidTargets(ref validTargets);
        float currentangle = Vector3.SignedAngle(target.position - transform.position, transform.forward, Vector3.up);

        //pointless to consider the current object
        validTargets.Remove(target.gameObject);
        if (validTargets.Count >= 1)
        {
            int counter = validTargets.Count - 1;
            float bestAngle = leftTrightF ? 500 : -500;

            while (counter >= 0)
            {
                float thisAngle = Vector3.SignedAngle(validTargets[counter].transform.position - transform.position, transform.forward, Vector3.up);
                //If its to the left but less to the left than the last
                if (leftTrightF && (thisAngle > currentangle && thisAngle < bestAngle) || !leftTrightF && (thisAngle < currentangle && thisAngle > bestAngle))
                {
                    target = validTargets[counter].transform;
                    bestAngle = thisAngle;

                }

                counter--;
            }
        }
        else { return; }


    }

    public static float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360F)
            angle += 360F;
        if (angle > 360F)
            angle -= 360F;
        return Mathf.Clamp(angle, min, max);
    }


}