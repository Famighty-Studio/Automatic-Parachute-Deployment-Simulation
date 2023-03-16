using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParachuteBehavior : MonoBehaviour
{
    public bool Inflated
    {
        set
        {
            animatorController.SetBool("inflated", value);
        }
    }
    [SerializeField] private Animator animatorController;
    [SerializeField] private Cloth clothSimulatorComponent;
    [SerializeField] private Transform groundChecker;
    [SerializeField] private float groundCheckerRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    private bool landed = false;

    public bool Grounded => landed;

    private void FixedUpdate()
    {
        if (Physics.CheckSphere(groundChecker.position, groundCheckerRadius, groundLayer) && !landed)
        {
            Landed();
        }
    }

    public void Landed()
    {
        clothSimulatorComponent.enabled = true;
        landed = true;
        StartCoroutine("DisableClothComponent");
    }

    private IEnumerator DisableClothComponent()
    {
        yield return new WaitForSeconds(5);
        clothSimulatorComponent.damping = 1;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundChecker != null)
        {
            Gizmos.DrawWireSphere(groundChecker.position, groundCheckerRadius);
        }
    }
}
