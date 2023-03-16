using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class Analyzer : MonoBehaviour
{
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
    [SerializeField] private float safeTimeToDeploy = 60f; // seconds
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
    [SerializeField] private float weight;
    [SerializeField] private float drag;
    [SerializeField] private float timeLeftBeforeGrounded;

    [Space]
    [SerializeField] private bool checkTerminalVelocity;
    [SerializeField] private bool printDragToConsole;
    [SerializeField] private bool printWeightToConsole;

    [Header("Required Components")]
    [SerializeField] private Transform parachuteTransform;
    [SerializeField] private ParachuteBehavior parachuteBehaviorComponent;
    [SerializeField] private SkinnedMeshRenderer parachuteMesh;
    [SerializeField] private Rigidbody rigidbodyComponent;

    private void OnValidate()
    {
		if (rigidbodyComponent == null) rigidbodyComponent = GetComponent<Rigidbody>();
        rigidbodyComponent.mass = mass;
        rigidbodyComponent.drag = loadDragCoefficient;
    }

    private void Update()
    {
        if (Input.GetKeyUp(KeyCode.Space))
        {
            isParachuteOpen = true;
        }
    }

    private void FixedUpdate()
    {
        if (!parachuteBehaviorComponent.Grounded) timeLeftBeforeGrounded = transform.position.y / velocity;

        if (!isParachuteOpen)
        {
            velocityBeforeInflation = rigidbodyComponent.velocity.magnitude;
        }

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

                parachuteTransform.localScale = Vector3.MoveTowards(parachuteTransform.localScale, Vector3.one, inflationTime * Time.deltaTime);

                if (parachuteMesh.GetBlendShapeWeight(0) < 15)
                {
                    parachuteBehaviorComponent.Inflated = true;
                }
            }
            else
            {
                inflated = true;
            }
        }

        CalculatePhysics();

        CalculateAutoDeployment();

        velocity = rigidbodyComponent.velocity.magnitude;
        if (printDragToConsole) PrintDragValue();
        if (printWeightToConsole) PrintWeightValue();
    }

    private void CalculatePhysics()
    {
        CalculateInternalPressure();
        // CalculateInflationTime();
        CalculateTerminalVelocity();
        
        if (velocity > terminalVelocity)
        {
            velocity = Mathf.MoveTowards(velocity, terminalVelocity, ((velocityBeforeInflation - velocityAfterInflation) / inflationTime) * Time.deltaTime);
            rigidbodyComponent.velocity = Vector3.down * velocity;
        }
        else rigidbodyComponent.velocity = Vector3.ClampMagnitude(rigidbodyComponent.velocity, terminalVelocity);
    }

    private void CalculateAutoDeployment()
    {
        CalculateTerminalVelocity();

        float _safeTimeDeployment = safeTimeToDeploy - inflationTime;

        if (timeLeftBeforeGrounded <= _safeTimeDeployment)
        {
            isParachuteOpen = true;
        }
    }

    private void CalculateInternalPressure()
    {
        localPressure = (density * Mathf.Pow(velocity, 2)) / 2;
    }

    private void CalculateInflationTime()
    {
        inflationTime = canopyFillConstant * (chuteCanopyDiameter / Mathf.Pow(velocityBeforeInflation, decellerationExponent));
    }

    private void CalculateTerminalVelocity()
    {
        terminalVelocity = Mathf.Sqrt(
                (2 * mass * (gravity / density)) / 
                ((isParachuteOpen ? (parachuteDragCoefficient * parachuteSurfaceArea) : 0) + (loadDragCoefficient * loadSurfaceArea))
            );
    }

    private void PrintWeightValue()
    {
        weight = mass * (velocity / Time.deltaTime);
		Debug.Log($"Weight: {weight}");
	}

    private void PrintDragValue()
    {
        drag = ((isParachuteOpen ? parachuteDragCoefficient : loadDragCoefficient) * density * Mathf.Pow(velocity, 2) * (isParachuteOpen ? parachuteSurfaceArea : loadSurfaceArea)) / 2;
        Debug.Log($"Calculated Drag: {drag}");
    }
}
