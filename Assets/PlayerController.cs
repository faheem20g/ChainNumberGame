using UnityEngine;
using System.Collections.Generic;
public class PlayerController : MonoBehaviour
{
    [Header("Snake Settings")]
    public int currentScore = 1;
    public float forwardSpeed = 10f;
    public float swerveSpeed = 5f; // Sensitivity for swiping
    public float horizontalSmoothTime = 0.1f; // Smoothing for horizontal movement
    public float maxHorizontalPos = 4.5f; // Boundary (approx 1.5 lanes each side if lane width is 3)
    public Transform bodySegmentPrefab; // Assign a Prefab with a Text/Number visual
    public float bodyGap = 1.0f; // Distance between segments
    public float bodyFollowSpeed = 10f; // Speed at which body parts follow their target positions
    public List<Transform> bodyParts = new List<Transform>();
    
    [Header("Visual Models")]
    public GameObject[] digitPrefabs = new GameObject[10]; // Assign digit 0-9 prefabs in inspector
    public Transform playerVisualContainer; // The child object that holds the current digit model
    public float digitSpacing = 0.3f;
    
    // Path History
    private List<Vector3> _positionHistory = new List<Vector3>();
    private float _lastFrameFingerPositionX;
    private float _targetHorizontalPos;
    private float _currentHorizontalVelocity;
    
    void Start()
    {
        _targetHorizontalPos = transform.localPosition.x;
        UpdateVisuals(); // Initialize the player's digit visual
    }

    private bool _isGameStarted = false;

    void Update()
    {
        if (!_isGameStarted)
        {
            if (Input.GetMouseButtonDown(0))
            {
                _isGameStarted = true;
            }
            else
            {
                return;
            }
        }

        // 1. Movement Logic
        HandleMovement();

        // 2. Record Path
        RecordHistory();

        // 3. Update Body Parts
        UpdateBodyParts();
    }

    void HandleMovement()
    {
        // Automatic Forward Movement
        transform.Translate(Vector3.forward * forwardSpeed * Time.deltaTime);

        // Horizontal Movement Input (Mouse/Touch)
        if (Input.GetMouseButton(0))
        {
            if (Input.GetMouseButtonDown(0))
            {
                _lastFrameFingerPositionX = Input.mousePosition.x;
            }
            else
            {
                // Calculate delta and update target position
                float deltaX = Input.mousePosition.x - _lastFrameFingerPositionX;
                _lastFrameFingerPositionX = Input.mousePosition.x;
                
                // Convert screen space delta to world space movement
                _targetHorizontalPos += deltaX * swerveSpeed * Time.deltaTime;
                _targetHorizontalPos = Mathf.Clamp(_targetHorizontalPos, -maxHorizontalPos, maxHorizontalPos);
            }
        }

        // Smoothly move to target position using SmoothDamp
        Vector3 currentPos = transform.localPosition;
        currentPos.x = Mathf.SmoothDamp(currentPos.x, _targetHorizontalPos, ref _currentHorizontalVelocity, horizontalSmoothTime);
        transform.localPosition = currentPos;
    }

    void RecordHistory()
    {
        // Record position every frame for smooth body following
        _positionHistory.Add(transform.position); 
        
        // Cleanup old history to prevent memory issues
        // Keep enough history for all body parts plus some buffer
        int maxHistorySize = Mathf.Max(100, bodyParts.Count * 50);
        if (_positionHistory.Count > maxHistorySize)
        {
            _positionHistory.RemoveRange(0, _positionHistory.Count - maxHistorySize);
        }
    }

    void UpdateBodyParts()
    {
        if(bodyParts.Count == 0) return;
        
        // For each body part, find the position in history that is the correct distance behind the head
        for (int bodyIndex = 0; bodyIndex < bodyParts.Count; bodyIndex++)
        {
            float targetDistance = (bodyIndex + 1) * bodyGap;
            Vector3 targetPosition = GetPositionAtDistance(targetDistance);
            Vector3 lookDirection = GetLookDirectionAtDistance(targetDistance);
            
            // Smoothly move body part to target position
            bodyParts[bodyIndex].position = Vector3.Lerp(
                bodyParts[bodyIndex].position, 
                targetPosition, 
                bodyFollowSpeed * Time.deltaTime
            );
            
            // Smoothly rotate to face movement direction
            if (lookDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                bodyParts[bodyIndex].rotation = Quaternion.Slerp(
                    bodyParts[bodyIndex].rotation,
                    targetRotation,
                    bodyFollowSpeed * Time.deltaTime
                );
            }
        }
    }
    
    Vector3 GetPositionAtDistance(float distance)
    {
        float totalDist = 0f;
        Vector3 lastPoint = transform.position;

        for (int i = _positionHistory.Count - 2; i >= 0; i--)
        {
            Vector3 point = _positionHistory[i];
            float segmentDist = Vector3.Distance(point, lastPoint);
            
            if (totalDist + segmentDist >= distance)
            {
                // Interpolate between lastPoint and point
                float remainingDist = distance - totalDist;
                float t = remainingDist / segmentDist;
                return Vector3.Lerp(lastPoint, point, t);
            }
            
            totalDist += segmentDist;
            lastPoint = point;
        }
        
        // If we run out of history, return the oldest point
        return _positionHistory.Count > 0 ? _positionHistory[0] : transform.position;
    }
    
    Vector3 GetLookDirectionAtDistance(float distance)
    {
        float totalDist = 0f;
        Vector3 lastPoint = transform.position;

        for (int i = _positionHistory.Count - 2; i >= 0; i--)
        {
            Vector3 point = _positionHistory[i];
            float segmentDist = Vector3.Distance(point, lastPoint);
            
            if (totalDist + segmentDist >= distance)
            {
                // Return direction from current point to next point
                return (lastPoint - point).normalized;
            }
            
            totalDist += segmentDist;
            lastPoint = point;
        }
        
        return Vector3.forward;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<NumberPickup>(out var pickup))
        {
            GrowSnake(pickup.value);
            Destroy(other.gameObject);
        }
        else if (other.TryGetComponent<Obstacle>(out var obstacle))
        {
            ShrinkSnake(obstacle.damage);
            // Optional: Feedback/Destroy obstacle
        }
        else if (other.TryGetComponent<LevelFinish>(out var finish))
        {
            Debug.Log("Level Finished!");
            enabled = false; // Stop movement
        }
    }

    void GrowSnake(int amount)
    {
        currentScore += amount;
        
        int targetBodyCount = currentScore - 1;
        int partsToAdd = targetBodyCount - bodyParts.Count;
        
        for (int i = 0; i < partsToAdd; i++)
        {
            // Spawn new parts at the last body part position or head position
            Vector3 spawnPos = bodyParts.Count > 0 ? bodyParts[bodyParts.Count - 1].position : transform.position;
            Transform newPart = Instantiate(bodySegmentPrefab, spawnPos, Quaternion.identity);
            bodyParts.Add(newPart);
        }
        
        UpdateVisuals();
    }
    
    void ShrinkSnake(int amount)
    {
        currentScore -= amount;
        if (currentScore < 1) 
        {
            currentScore = 1; // Or Game Over
        }

        int targetBodyCount = currentScore - 1;
        while (bodyParts.Count > targetBodyCount)
        {
            Transform lastPart = bodyParts[bodyParts.Count - 1];
            bodyParts.RemoveAt(bodyParts.Count - 1);
            Destroy(lastPart.gameObject);
        }
        
        UpdateVisuals();
    }

    void UpdateVisuals()
    {
        // Update player head visual
        SwapDigitModel(playerVisualContainer, currentScore);
        name = "Head_" + currentScore;
        
        // Update body parts visuals
        for (int i = 0; i < bodyParts.Count; i++)
        {
            int val = currentScore - 1 - i;
            
            // Find or create visual container for this body part
            Transform bodyVisualContainer = bodyParts[i].Find("VisualContainer");
            if (bodyVisualContainer == null)
            {
                GameObject container = new GameObject("VisualContainer");
                container.transform.SetParent(bodyParts[i], false);
                bodyVisualContainer = container.transform;
            }
            
            SwapDigitModel(bodyVisualContainer, val);
            bodyParts[i].name = "Body_" + val;
        }
    }
    
    void SwapDigitModel(Transform container, int numberValue)
    {
        if (container == null)
        {
            Debug.LogWarning("Visual container is null! Make sure to assign playerVisualContainer in the inspector.");
            return;
        }

        // Destroy old visual
        foreach (Transform child in container)
        {
            Destroy(child.gameObject);
        }
        
        string numberStr = numberValue.ToString();
        int digitCount = numberStr.Length;
        
        // Calculate starting X position so the whole number is centered
        // Layout:  (N-1) spaces * spacing
        // Start from -totalWidth/2
        float totalWidth = (digitCount - 1) * digitSpacing;
        float startX = -totalWidth / 2f;

        for (int i = 0; i < digitCount; i++)
        {
            int digit = int.Parse(numberStr[i].ToString());
            
            // Check if we have the prefab
            if (digit >= 0 && digit < digitPrefabs.Length && digitPrefabs[digit] != null)
            {
                 GameObject newDigit = Instantiate(digitPrefabs[digit], container);
                 
                 // Position based on index
                 float xPos = startX + (i * digitSpacing);
                 newDigit.transform.localPosition = new Vector3(xPos, 0, 0);
                 newDigit.transform.localRotation = Quaternion.identity;
                 newDigit.transform.localScale = Vector3.one;
            }
            else
            {
                 Debug.LogWarning($"Digit prefab for {digit} is not assigned or invalid!");
            }
        }
    }
}
