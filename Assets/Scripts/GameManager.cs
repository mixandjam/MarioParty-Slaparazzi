using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;
using Cinemachine;
using UnityEngine.EventSystems;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("Game Settings")]
    [SerializeField] private int currentRound;
    [SerializeField] private int roundsAmount = 5;
    [SerializeField] private int cameraSpotsAmount = 5;
    private readonly int[]  possibleScores = new int[] { 3, 2, 1, 0 };
    private readonly Color[] playerColors = new Color[] { Color.red, Color.blue, Color.yellow, Color.black };
    public VertexGradient[] playerGradients;

    [Space]

    [Header("Scores")]
    [SerializeField] private List<PlayerScore> currentPlayerScores;
    [SerializeField] private List<PlayerScore> finalPlayerScores;

    [Space]

    [Header("Player Settings")]
    [SerializeField] private Transform playerHolder;
    private MovementInput[] allPlayers;
    private List<MovementInput> aiMovementScripts = new List<MovementInput>();
    [SerializeField] private LayerMask playerLayer;

    [Space]

    [Header("Camera Settings")]

    public Camera renderTextureCamera;
    public Transform cameraPlacements;
    public Transform cameraPivot;
    private Transform cameraCharacter;
    private Coroutine photoCoroutine;
    public Light cameraLight;
    public Volume dofVolume;
    public CinemachineTargetGroup targetGroup;

    [Space]

    [Header("UI References")]
    [SerializeField] private CanvasGroup gameUI;
    [SerializeField] private Transform pictureInterface;
    [SerializeField] private Transform pictureScoreTextHolder;
    [SerializeField] private Transform playerScoreTextHolder;
    [SerializeField] private Transform roundImageHolder;

    private void Awake()
    {
        instance = this;

        allPlayers = new MovementInput[4];

        //reference all the movement scripts
        for (int i = 0; i < playerHolder.childCount; i++)
            allPlayers[i] = playerHolder.GetChild(i).GetComponent<MovementInput>();

        //reference all the AI player movement scripts
        foreach (MovementInput movement in allPlayers)
            if (movement.isAI)
                aiMovementScripts.Add(movement);

        //create a list with all the scores
        for (int i = 0; i < allPlayers.Length; i++)
            finalPlayerScores.Add(new PlayerScore() { id = i, distance = 0 });
    }

    void Start()
    {
        cameraCharacter = cameraPivot.GetChild(0);

        //activate UI
        gameUI.gameObject.SetActive(true);

        //create camera spots
        for (int i = 0; i < cameraSpotsAmount; i++)
        {
            GameObject cameraReference = new GameObject();
            cameraReference.transform.parent = cameraPlacements;
            cameraReference.transform.localEulerAngles = new Vector3(0, (360 / cameraSpotsAmount) * i, 0);
        }

        ChooseRandomPhotoSpot(true);

        cameraCharacter.localPosition = Vector3.forward * 14;
        pictureInterface.localScale = Vector3.zero;

        StartCoroutine(CountdownSequence());

        //initial camera movement and UI fade
        DOVirtual.Float(1, 0, 3, SetInitialTargetWeight).SetEase(Ease.InOutBack).OnComplete(()=>gameUI.DOFade(1,.5f));

        IEnumerator CountdownSequence()
        {
            print("3"); yield return new WaitForSeconds(1); print("2"); yield return new WaitForSeconds(1); print("1");
            yield return new WaitForSeconds(1);
            print("GO!");

            StartCoroutine(GameSequence());

        }

        IEnumerator GameSequence()
        {
            currentRound++;
            EventSystem.current.SetSelectedGameObject(roundImageHolder.GetChild(currentRound - 1).gameObject);

            SetAgentsDestination(Vector3.zero, true);
            ChooseRandomPhotoSpot(false);

            StartCoroutine(AgentDestinationWait(.7f, cameraPivot.GetChild(1).position));

            foreach (MovementInput player in allPlayers)
                player.waiting = false;

            //animation
            cameraCharacter.localPosition = Vector3.forward * 14;
            cameraCharacter.DOLocalMoveZ(6, 1);

            DOVirtual.Float(0, 1, 2, SetTargetWeight).SetDelay(2);

            yield return new WaitForSeconds(5);
            TakePicture();
            foreach (MovementInput player in allPlayers)
                player.waiting = true;

            DOVirtual.Float(1, 0, 1, SetTargetWeight).SetDelay(2);

            yield return new WaitForSeconds(3);

            cameraCharacter.DOLocalMoveZ(20, 1);

            yield return new WaitForSeconds(1.5f);

            if (currentRound < roundsAmount)
                StartCoroutine(GameSequence());
            else
                print("finish");
        }
    }

    void SetTargetWeight(float weight)
    {
        targetGroup.m_Targets[1].weight = weight;
        for (int i = 0; i < allPlayers.Length; i++)
        {
            allPlayers[i].headLookRig.weight = weight;
        }
    }

    void SetInitialTargetWeight(float weight)
    {
        targetGroup.m_Targets[2].weight = weight;
    }

    void ChooseRandomPhotoSpot(bool start)
    {
        List<Transform> options = new List<Transform>();
        for (int i = 0; i < cameraPlacements.childCount; i++)
        {
            if(!start && cameraPlacements.GetChild(i).localEulerAngles == cameraPivot.localEulerAngles)
            {
                //don't add
            }
            else
            {
                options.Add(cameraPlacements.GetChild(i).transform);
            }
        }

        cameraPivot.localEulerAngles = options[Random.Range(0, options.Count)].localEulerAngles;

    }

    void TakePicture()
    {
        //clear the list
        currentPlayerScores.Clear();

        RaycastHit hit;

        //populate the list with the players and their distance to the camera
        for (int i = 0; i < allPlayers.Length; i++)
        {
            allPlayers[i].canMove = false;

            Vector3 camPos = cameraPivot.GetChild(2).position;
            Vector3 playerPos = allPlayers[i].transform.position;
            PlayerScore score = new PlayerScore() { id = i };
            score.distance = Vector3.Distance(playerPos, camPos);
            score.position = playerPos;

            //raycast logic
            if (Physics.Raycast(camPos, (allPlayers[i].transform.position - camPos) + Vector3.up, out hit, 5, playerLayer))
                if (hit.transform == allPlayers[i].transform)
                    score.cameraVisible = true;

            if (!allPlayers[i].insideCameraTrigger)
                score.cameraVisible = false;

            if (score.cameraVisible)
                currentPlayerScores.Add(score);
        }

        //order the list based on the players distance to the camera
        currentPlayerScores = currentPlayerScores.OrderBy(s => s.distance).ToList();

        //add to the final score list based on the order of the current list
        for (int i = 0; i < currentPlayerScores.Count; i++)
        {
            PlayerScore player = finalPlayerScores.Find(x => x.id == currentPlayerScores[i].id);
            int playerIndex = finalPlayerScores.IndexOf(player);
            player.score += possibleScores[i];
            finalPlayerScores[playerIndex] = player;
        }

        //stop agents from moving
        SetAgentsDestination(Vector3.zero, true);

        cameraLight.DOIntensity(350, .02f).OnComplete(() => cameraLight.DOIntensity(0, .05f));
        cameraLight.transform.parent.DOPunchScale(Vector3.one / 3, .5f, 10, 1);

        WaitForSeconds intervalWait = new WaitForSeconds(.1f);
        WaitForEndOfFrame frameWait = new WaitForEndOfFrame();

        StartCoroutine(TakePhoto());

        IEnumerator TakePhoto()
        {
            yield return intervalWait;

            renderTextureCamera.gameObject.SetActive(true);

            SetPhotoScorePosition(true);

            yield return frameWait;

            renderTextureCamera.gameObject.SetActive(false);

            Sequence s = DOTween.Sequence();
            s.Append(pictureInterface.DOScale(1, .4f).SetEase(Ease.OutBounce));
            s.AppendInterval(1);
            for (int i = 0; i < currentPlayerScores.Count; i++)
            {
                if (i > 2)
                    break;
                s.Join(pictureScoreTextHolder.GetChild(i).DOScale(1, .2f).SetEase(Ease.OutBack));
            }
            s.AppendInterval(1);
            for (int i = 0; i < currentPlayerScores.Count; i++)
            {
                if (i > 2)
                    break;
                s.Join(pictureScoreTextHolder.GetChild(i).DOMove(playerScoreTextHolder.GetChild(currentPlayerScores[i].id).position, .4f)
                    .OnComplete(() => UpdateScore()));
            }
            s.Append(pictureInterface.DOScale(0, .4f).SetEase(Ease.InBack));
            s.AppendCallback(() => SetPhotoScorePosition(false));
        }
    }

    IEnumerator AgentDestinationWait(float wait, Vector3 destination)
    {
        yield return new WaitForSeconds(Random.Range(wait - .2f, wait + .3f));

        SetAgentsDestination(destination, false);

    }

    void SetAgentsDestination(Vector3 destination, bool stop)
    {
        foreach (MovementInput aiMovement in aiMovementScripts)
        {
            aiMovement.SetAgentDestination(stop ? aiMovement.transform.position : destination);
            aiMovement.canHit = !stop;
        }
    }

    void UpdateScore()
    {
        for (int i = 0; i < pictureScoreTextHolder.childCount; i++)
        {
            pictureScoreTextHolder.GetChild(i).DOScale(0, .2f);
        }

        for (int i = 0; i < currentPlayerScores.Count; i++)
        {
            int id = currentPlayerScores[i].id;
            playerScoreTextHolder.GetChild(id).DOComplete();
            playerScoreTextHolder.GetChild(id).DOPunchScale(Vector3.one/2, .2f, 10, 1);
            PlayerScore p = finalPlayerScores.Find(x => x.id == id);
            playerScoreTextHolder.GetChild(id).GetComponentInChildren<TextMeshProUGUI>().text = p.score.ToString();
        }
    }
    void SetPhotoScorePosition(bool show)
    {
        if (show)
        {
            for (int i = 0; i < currentPlayerScores.Count; i++)
            {
                if (i > 2)
                    break;

                PlayerScore score = currentPlayerScores[i];
                Vector3 pos = allPlayers[currentPlayerScores[i].id].transform.position;
                TextMeshProUGUI textMesh = pictureScoreTextHolder.GetChild(i).GetComponent<TextMeshProUGUI>();
                //textMesh.color = playerColors[score.id];
                textMesh.colorGradient = playerGradients[score.id];
                Vector3 playerPosInPhoto = renderTextureCamera.WorldToScreenPoint(pos + Vector3.up);
                pictureScoreTextHolder.GetChild(i).position = new Vector3((playerPosInPhoto.x / 3) + 700, playerPosInPhoto.y, playerPosInPhoto.z);
            }
        }
        else
        {
            for (int i = 0; i < pictureScoreTextHolder.childCount; i++)
            {
                pictureScoreTextHolder.GetChild(i).localScale = Vector3.zero;
            }
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;

        if (!Application.isPlaying)
            return;

        for (int i = 0; i < 4; i++)
        {
            Gizmos.DrawRay(cameraPivot.GetChild(0).position, (allPlayers[i].transform.position - cameraPivot.GetChild(0).position) + Vector3.up);
        }
    }

}

[System.Serializable]
public struct PlayerScore
{
    public int id;
    public float distance;
    public int score;
    public bool cameraVisible;
    public Vector3 position;
}
