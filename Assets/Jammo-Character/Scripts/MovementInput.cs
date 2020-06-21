
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using DG.Tweening;
using UnityEngine.Animations.Rigging;

public class MovementInput : MonoBehaviour {

	[Header("Artificial Inteligence")]
	public bool isAI;
	private NavMeshAgent agent;
	private float agentOriginalSpeed;
	[Space]

	[Header("States")]
	public bool canHit;
	public bool canMove;
	public bool stunned;
	public bool waiting;
	public bool insideCameraTrigger;
	[Space]

	[SerializeField] private float speed = 5;
	[SerializeField] private bool blockRotationPlayer;
	[SerializeField] private float desiredRotationSpeed = 0.1f;
	[SerializeField] private float allowPlayerRotation = 0.1f;

	private float InputX;
	private float InputZ;
	private Vector3 desiredMoveDirection;
	public Vector3 bashDirection;
	private Animator anim;
	private Camera cam;

	public LayerMask rayLayerMask;

    [Header("Animation Smoothing")]
    [Range(0, 1f)]
    public float HorizontalAnimSmoothTime = 0.2f;
    [Range(0, 1f)]
    public float VerticalAnimTime = 0.2f;
    [Range(0,1f)]
    public float StartAnimTime = 0.3f;
    [Range(0, 1f)]
    public float StopAnimTime = 0.15f;
	public Rig headLookRig;

	[Space]

	[Header("Polish")]
	public Renderer characterEye;

	// Use this for initialization
	void Start () {
		anim = this.GetComponent<Animator> ();
		cam = Camera.main;
		agent = GetComponent<NavMeshAgent>();
		agentOriginalSpeed = agent.speed;
		canHit = true;
		canMove = true;
	}
	
	// Update is called once per frame
	void Update () {

		if (Input.GetKeyDown(KeyCode.P) && !isAI)
		{
			anim.SetTrigger("bash");
			//polish
			StartCoroutine(SetTargetEye(1, new Vector2(.99f, -.33f)));
		}

		if (stunned)
			StunMovement();

		if (!isAI)
		{
			InputMagnitude();
		}
		else
		{
			anim.SetFloat("Blend", agent.velocity.magnitude, StartAnimTime, Time.deltaTime);
		}


		if (!isAI)
			return;

		Collider[] playerColliders = Physics.OverlapSphere(transform.position + transform.forward, .4f, rayLayerMask);

		if(playerColliders.Length > 0 && canHit && Vector3.Distance(transform.position, GameManager.instance.cameraPivot.GetChild(0).position) < 3 && !waiting)
		{
			anim.SetTrigger("bash");
			//polish
			StartCoroutine(SetTargetEye(1, new Vector2(.66f, 0)));

			canHit = false;
			agent.speed = 0;

			StartCoroutine(HitCooldown());
			StartCoroutine(MoveCooldown());

			IEnumerator HitCooldown()
			{
				yield return new WaitForSeconds(1.5f);
				canHit = true;
			}

			IEnumerator MoveCooldown()
			{
				yield return new WaitForSeconds(.7f);
				agent.speed = agentOriginalSpeed;
			}
		}
	}

	public void SetAgentDestination(Vector3 destination)
	{
		agent.SetDestination(destination);
	}

	void StunMovement()
	{
		Vector3 newPosition = transform.position + bashDirection * Time.deltaTime * 3.5f;
		NavMeshHit navhit;
		bool isValid = NavMesh.SamplePosition(newPosition, out navhit, .3f, NavMesh.AllAreas);

		if (!isValid)
			return;

		transform.position = navhit.position;
	}

    void PlayerMoveAndRotation() {


		InputX = Input.GetAxis ("Horizontal");
		InputZ = Input.GetAxis ("Vertical");
		Vector3 axis = new Vector3(InputX, 0, InputZ);

		//camera conversion
		var forward = cam.transform.forward;
		var right = cam.transform.right;

		forward.y = 0f;
		right.y = 0f;

		forward.Normalize ();
		right.Normalize ();

		desiredMoveDirection = forward * InputZ + right * InputX;

		//navmesh movement

		if (axis.magnitude < .01f)
			return;

		Vector3 newPosition = transform.position + desiredMoveDirection * Time.deltaTime * speed;
		NavMeshHit hit;
		bool isValid = NavMesh.SamplePosition(newPosition, out hit, .3f, NavMesh.AllAreas);

		if (!isValid)
			return;

		if ((transform.position - hit.position).magnitude >= .02f)
			transform.position = hit.position;

		//rotation
		transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(desiredMoveDirection), desiredRotationSpeed);
	}


	void InputMagnitude() {

		if (!canMove)
		{
			anim.SetFloat("Blend", 0, StartAnimTime, Time.deltaTime);
			return;
		}

		//Calculate Input Vectors
		InputX = Input.GetAxis ("Horizontal");
		InputZ = Input.GetAxis ("Vertical");

		//Calculate the Input Magnitude
		float inputMagnitude = new Vector2(InputX, InputZ).sqrMagnitude;

        //Physically move player
		if (inputMagnitude > allowPlayerRotation) {
			anim.SetFloat ("Blend", inputMagnitude, StartAnimTime, Time.deltaTime);
			PlayerMoveAndRotation ();
		} else if (inputMagnitude < allowPlayerRotation) {
			anim.SetFloat ("Blend", inputMagnitude, StopAnimTime, Time.deltaTime);
		}
	}

	public void SetStunned(Vector3 dir)
	{
		bashDirection = dir;
		anim.SetTrigger("hit");

		StartCoroutine(SetTargetStunned(1));

		IEnumerator SetTargetStunned(float time)
		{
			GetComponent<MovementInput>().stunned = true;
			GetComponent<MovementInput>().canMove = false;
			agent.speed = 0;
			yield return new WaitForSeconds(time);
			agent.speed = agentOriginalSpeed;
			GetComponent<MovementInput>().stunned = false;
			GetComponent<MovementInput>().canMove = true;
		}

		//polish
		StartCoroutine(SetTargetEye(1, new Vector2(.99f, -.33f)));

	}

	//polish
	IEnumerator SetTargetEye(float time, Vector2 eyeOffset)
	{
		characterEye.material.SetTextureOffset("_BaseMap", eyeOffset);
		yield return new WaitForSeconds(time);
		characterEye.material.SetTextureOffset("_BaseMap", Vector2.zero);
	}

	private void OnTriggerEnter(Collider other)
	{
		if (other.CompareTag("Player") && other.transform != transform && !stunned)
		{
			if(GetComponentInChildren<ParticleSystem>() != null)
				GetComponentInChildren<ParticleSystem>().Play();
			other.GetComponent<MovementInput>().SetStunned(transform.forward);
		}

		if (other.CompareTag("Camera"))
		{
			insideCameraTrigger = true;
		}
	}

	private void OnTriggerExit(Collider other)
	{
		if (other.CompareTag("Camera"))
		{
			insideCameraTrigger = false;
		}
	}

	private void OnDrawGizmos()
	{
		Gizmos.color = Color.red;
		Vector3 origin = transform.position + (transform.forward);
		//Gizmos.DrawWireSphere(origin, .4f);
	}
}
