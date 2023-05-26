using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;

public class Analyzer : MonoBehaviour
{
    [Serializable] public enum AutoDeploymentParameter { Altitude, ImpactForce, TimeToGrounded}

    [Header("Object Properties")]
    [SerializeField] private float mass; // kg
    [SerializeField] private float parachuteSurfaceArea; // m2
    [SerializeField] private float loadSurfaceArea; // m2
    [SerializeField] private float characteristicLength; // m
    [SerializeField] private float parachuteDragCoefficient; // constant
    [SerializeField] private float loadDragCoefficient; // constant
    [SerializeField] private bool isParachuteOpen = false;

    [Space] 
    [SerializeField] private float gravity = Physics.gravity.magnitude;

    [Header("Opening Load of The Parachute")]
	[SerializeField] private AutoDeploymentParameter autoDeploymentParameter;
    [SerializeField] private float safealtitudeToDeploy = 2500f;
    [SerializeField] private float safeImpactForceToDeploy = 20f;
	[SerializeField] private float safeTimeToDeploy = 60f; // seconds
    [SerializeField] private bool autoCalculateSafetyDeployment = false;
    [Space]
    [SerializeField] private float localPressure; // Pa
    [SerializeField] private float canopyFillConstant; // constant
    [SerializeField] private float decellerationExponent; // constant
    [SerializeField] private float chuteCanopyDiameter; // m
    [SerializeField] private float inflationTime; // s 
    [SerializeField] private bool inflated; 

    [Header("Velocities")]
    [SerializeField] private float terminalVelocity; // m/s
    [SerializeField] private float velocity; // m/s

    [Space]
    [SerializeField] private float velocityBeforeInflation;
    [SerializeField] private float velocityAfterInflation;

    [Header("Air Properties")]
    [SerializeField] private float density; // kg/m3
    [SerializeField] private float dynamicViscosity; // kg/m/s || Pa s

    [Header("Debug")]
    [SerializeField] private float weight; // N
    [SerializeField] private float drag; // N
    [SerializeField] private float timeLeftBeforeGrounded; // s
    [SerializeField] private float maximumImpactForce; // N

    [Space]
    [SerializeField] private bool checkTerminalVelocity;
    [SerializeField] private bool printDragToConsole;
    [SerializeField] private bool printWeightToConsole;

    [Header("Required Components")]
    [SerializeField] private Transform parachuteTransform;
    [SerializeField] private ParachuteBehavior parachuteBehaviorComponent;
    [SerializeField] private SkinnedMeshRenderer parachuteMesh;
    [SerializeField] private CharacterController rigidbodyComponent;

    private Vector3 motion;
    private float cachedImpactForce;
    private float initialAltitude; // m

    private void OnValidate()
    {
        // Initiate the Character controller as a rigidbody component
        // this controller will control the physics behavior of the load
		if (rigidbodyComponent == null) rigidbodyComponent = GetComponent<CharacterController>();
    }

	private void Start()
	{
        // set the initial altitude once the simulation is started
        initialAltitude = transform.position.y;
	}

	private void Update()
    {
        // manually open the parachute in case needed
        if (Input.GetKeyUp(KeyCode.Space))
        {
            isParachuteOpen = true;
        }
    }

    private void FixedUpdate()
    {
        // calculate the time left before the load is reached the ground
        if (!parachuteBehaviorComponent.Grounded) timeLeftBeforeGrounded = transform.position.y / velocity;

        // cache the velocity before the parachute is inflated
        if (!isParachuteOpen)
        {
            velocityBeforeInflation = rigidbodyComponent.velocity.magnitude;
        }

        // the inflation process
        if (isParachuteOpen && !inflated)
        {
            CalculateInflationTime();
            velocityAfterInflation = terminalVelocity;

			if (parachuteMesh.GetBlendShapeWeight(0) > 0)
            {
                parachuteMesh.SetBlendShapeWeight(0,
                    Mathf.MoveTowards(parachuteMesh.GetBlendShapeWeight(0),
                    0,
                    (velocityBeforeInflation - velocityAfterInflation) / inflationTime)
                    );

                if (parachuteMesh.GetBlendShapeWeight(0) < 15)
                {
                    parachuteBehaviorComponent.Inflated = true;
                }
            }
            else
            {
                inflated = true;
                printDragToConsole = true;
            }
        }

        // manage the size of the parachute while its deploying
        if (isParachuteOpen) 
            parachuteTransform.localScale = 
                Vector3.MoveTowards(parachuteTransform.localScale, Vector3.one, inflationTime * Time.deltaTime);

        // calculate the physics behavior of the load
		CalculatePhysics();

        // calculate the auto deployment parameters
        CalculateAutoDeployment();

        // debug only
        if (printDragToConsole) PrintDragValue();
        if (printWeightToConsole) PrintWeightValue();

        // cache the impact force during the fall
        cachedImpactForce = maximumImpactForce;
    }

    private void CalculatePhysics()
    {
        // get the internal or the dynamic pressure as well as the terminal velocity of the load
        CalculateInternalPressure();
        CalculateTerminalVelocity();

        // calculating impact force
        maximumImpactForce =  (mass * Mathf.Pow(velocity, 2)) / (2 * initialAltitude * Time.deltaTime);

        // make sure the velocity is calculated correctly and prevent the over flow
		if (rigidbodyComponent.isGrounded)
        {
            velocity = 0;
        }
        else
        {
			if (velocity > terminalVelocity)
			{
				velocity = Mathf.Lerp(velocity, terminalVelocity, (inflationTime) * Time.deltaTime);
				print(Math.Abs(velocity - terminalVelocity));
				if (Math.Abs(velocity - terminalVelocity) < 0.1f)
				{
					velocity = terminalVelocity - 0.1f;
				}
			}
			else
			{
				velocity = Mathf.Lerp(velocity, terminalVelocity, gravity * Time.deltaTime * Time.deltaTime);
			}
		}
        
        // set the motion as the velocity is changing
        motion = Vector3.down * velocity;

        // apply the motion to the rigidbody component to control the load physics behavior
		rigidbodyComponent.Move(motion * Time.deltaTime);
    }

    private void CalculateAutoDeployment()
    {
        // recheck the terminal velocity
        CalculateTerminalVelocity();

        // only run the lines below based on the if condition
        if (!autoCalculateSafetyDeployment)
        {
            switch (autoDeploymentParameter)
            {
                case AutoDeploymentParameter.TimeToGrounded:
                    {
						float _safeTimeDeployment = safeTimeToDeploy - inflationTime;

						if (timeLeftBeforeGrounded <= _safeTimeDeployment)
						{
							isParachuteOpen = true;
						}

                        break;
					}
                case AutoDeploymentParameter.Altitude:
                    {
						if (transform.position.y <= safealtitudeToDeploy)
						{
							isParachuteOpen = true;
						}

						break;
					}
                case AutoDeploymentParameter.ImpactForce:
                    {
						if (maximumImpactForce >= safeImpactForceToDeploy)
						{
							isParachuteOpen = true;
						}

						break;
                    }
            }
        }
        else
        {
            if (Mathf.Abs(velocity - terminalVelocity) < 0.1f && 
                Mathf.Abs(timeLeftBeforeGrounded - (transform.position.y / velocity)) < 0.1f &&
				Mathf.Abs(maximumImpactForce - cachedImpactForce) < 0.1f)
            {
				isParachuteOpen = true;
			}
        }
    }

    // calculating the internal or the dynamic pressure
    private void CalculateInternalPressure()
    {
        localPressure = (density * Mathf.Pow(velocity, 2)) / 2;
    }

    // calculating the inflation time
    private void CalculateInflationTime()
    {
        inflationTime = canopyFillConstant * (chuteCanopyDiameter / Mathf.Pow(velocityBeforeInflation, decellerationExponent));
    }

    // calculating the terminal velocity
    private void CalculateTerminalVelocity()
    {
        terminalVelocity = Mathf.Sqrt(
                (2 * mass * (gravity / density)) / 
                ((isParachuteOpen ? (parachuteDragCoefficient * parachuteSurfaceArea) : 0) + (loadDragCoefficient * loadSurfaceArea))
            );
    }

    // print the weight value to the console
    private void PrintWeightValue()
    {
        weight = mass * gravity;
		// Debug.Log($"Weight: {weight}");
	}

    // print the drag value to the console
    private void PrintDragValue()
    {
        if (isParachuteOpen)
            drag = parachuteDragCoefficient * density * Mathf.Pow(velocity, 2) * parachuteSurfaceArea / 2;
        else
			drag = loadDragCoefficient * density * Mathf.Pow(velocity, 2) * loadSurfaceArea / 2;
		// Debug.Log($"Calculated Drag: {drag}");
	}

    // this region is only to show the calculated values into the screen
	private void OnGUI()
	{
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(); 
        GUILayout.Label(" Initial Altitude (m): ");
		GUILayout.Label(" Altitude (m): ");
		GUILayout.Label(" Velocity (m/s): ");
		GUILayout.Label(" Terminal Velocity (m/s): ");
		GUILayout.Label(" Inflation Time (s): ");
		GUILayout.Label(" Time Before Grounded (s): ");
		GUILayout.Label(" Weight (N): ");
		GUILayout.Label(" Drag Force (N): ");
		GUILayout.Label(" Maximum Impact Force (N): ");
		if (inflated)
		{
			GUILayout.Label("Dynamic Pressure (Pa): ");
		}
		GUILayout.EndVertical();
		GUILayout.BeginVertical();
		GUILayout.Label(initialAltitude.ToString());
		GUILayout.Label(transform.position.y.ToString());
		GUILayout.Label(velocity.ToString());
		GUILayout.Label(terminalVelocity.ToString());
		GUILayout.Label(inflationTime.ToString());
		GUILayout.Label(timeLeftBeforeGrounded.ToString());
		GUILayout.Label((mass * gravity).ToString());
		GUILayout.Label(drag.ToString());
		GUILayout.Label(maximumImpactForce.ToString());
		if (inflated)
		{
			GUILayout.Label(localPressure.ToString());
		}

		GUILayout.EndVertical();
		GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Simulation Speed");
        if (GUILayout.Button("1x"))
        {
            Time.timeScale = 1;
        }
		if (GUILayout.Button("2x"))
		{
			Time.timeScale = 2;
		}
		if (GUILayout.Button("5x"))
		{
			Time.timeScale = 5;
		}
		if (GUILayout.Button("10x"))
		{
			Time.timeScale = 10;
		}
		GUILayout.EndHorizontal();
	}
}
