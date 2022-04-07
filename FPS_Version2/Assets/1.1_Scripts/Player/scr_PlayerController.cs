﻿using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

public class scr_PlayerController : MonoBehaviourPunCallbacks
{
    #region - Variables -
    [SerializeField] [Header("滑鼠水平靈敏度")] float mouseSensitivity_X;
    [SerializeField] [Header("滑鼠垂直靈敏度")] float mouseSensitivity_Y;
    [SerializeField] [Header("當前 - 速度")] float currentSpeed;
    [SerializeField] [Header("走路 - 速度")] float walkSpeed;
    [SerializeField] [Header("跑步 - 速度")] float runSpeed;
    [SerializeField] [Header("滑行 - 速度")] float slideSpeed;
    [SerializeField] [Header("蹲下 - 速度")] float crouchSpeed;
    [SerializeField] [Header("瞄準 - 速度")] float aimSpeed;
    [SerializeField] [Header("移動滑順 - 時間")] float moveSmoothTime;
    [SerializeField] [Header("滑行滑順 - 時間")] float slideSmoothTime;
    [SerializeField] [Header("可滑行 - 時間")] float slide_time;
    [SerializeField] [Header("跳躍力道")] float jumpForce;
    [SerializeField] [Header("最大血量")] int maxHealth;
    [SerializeField] [Header("當前血量")] int currentHealth;

    [SerializeField] [Header("站立 - 碰撞器")] GameObject standCollider;
    [SerializeField] [Header("蹲下 - 碰撞器")] GameObject crounchCollider;

    [SerializeField] [Header("武器座標")] Transform weapon_Trans;
    [SerializeField] [Header("發射點座標")] Transform shoot_Trans;

    [Header("攝影機座標")] public GameObject cameraHolder;
    [Header("玩家攝影機")] public Camera playerCamera;

    [HideInInspector] public bool isGrounded;

    bool isMoving = false;              // 是否在跑步
    bool isRunning = false;             // 是否在跑步
    bool isSliding = false;             // 是否在滑行
    bool isCrouching = false;           // 是否在滑行
    bool paused = false;

    float lookRotation;                 // 上下視角旋轉值
    float walkFOV;                      // 走路視野
    float runFOV;                       // 跑步視野
    float counter;                      // 呼吸武器變數
    float slideTime;                    // 滑行時間

    Vector3 moveSmoothVelocity;         // 移動滑順加速度
    Vector3 direction;                  // 鍵盤座標量
    Vector3 moveDir;                    // 移動到的位置
    Vector3 target_weapon_Trans;        // 武器目標座標
    Vector3 camera_origin;
    Vector3 weapon_origin;
    Vector3 shoot_origin;

    Text ammo_UI;
    Image healthBar;
    Rigidbody rig;
    scr_Weapon scr_weapon;
    scr_GameManager scr_gameManager;
    #endregion

    #region - Monobehaviour -
    void Awake()
    {
        weapon_Trans = transform.GetChild(2).transform;
        shoot_Trans = transform.GetChild(3).transform;
        playerCamera = transform.GetChild(1).GetChild(0).GetComponent<Camera>();
        scr_weapon = GetComponent<scr_Weapon>();
        scr_gameManager = GameObject.Find("GameManager").GetComponent<scr_GameManager>();
        healthBar = GameObject.Find("HUD/血量顯示器/Health/bar").GetComponent<Image>();
        ammo_UI = GameObject.Find("HUD/子彈/Text").GetComponent<Text>();
        rig = GetComponent<Rigidbody>();
    }

    void Start()
    {
        cameraHolder.SetActive(photonView.IsMine);

        if (!photonView.IsMine) gameObject.layer = 11;

        walkFOV = playerCamera.fieldOfView;
        runFOV = walkFOV * 1.15f;
        currentHealth = maxHealth;
        slideTime = slide_time;
        camera_origin = cameraHolder.transform.localPosition;
        weapon_origin = weapon_Trans.localPosition;
        shoot_origin = shoot_Trans.localPosition;
    }

    void Update()
    {
        // 只控制自己生成的物件
        if (!photonView.IsMine) return;
        Pause();

        if (scr_SceneManager.paused) return;
        Cursor.lockState = CursorLockMode.Locked;

        Move();
        Slide();
        Crounch();
        View();
        Jump();
        UpdateHpBar();
        UpdateAmmo();
        BreathSwitch();
        CalculateSpeed();
    }

    void FixedUpdate()
    {
        Movement();
    }

    void OnTriggerEnter(Collider collider)
    {
        if (collider.gameObject.name == "地圖外")
        {
            Die();
        }
    }
    #endregion

    #region - RPC - 
    /// <summary>
    /// 改變座標位置
    /// </summary>
    /// <param name="camera_offset">相機位移量</param>
    /// <param name="time">時間</param>
    [PunRPC]
    void ChangePosition(float camera_offset, float time)
    {
        Vector3 camera_temp = new Vector3(camera_origin.x, camera_origin.y - camera_offset, camera_origin.z);
        cameraHolder.transform.localPosition = Vector3.Lerp(cameraHolder.transform.localPosition, camera_temp, Time.deltaTime * time);
        weapon_Trans.localPosition = Vector3.Lerp(weapon_Trans.localPosition, camera_temp, Time.deltaTime * time);
        shoot_Trans.localPosition = Vector3.Lerp(shoot_Trans.localPosition, cameraHolder.transform.localPosition, Time.deltaTime * time);
    }

    /// <summary>
    /// 座標回歸
    /// </summary>
    /// <param name="time">時間</param>
    [PunRPC]
    void ResetPosition(float time)
    {
        cameraHolder.transform.localPosition = Vector3.Lerp(cameraHolder.transform.localPosition, camera_origin, Time.deltaTime * time);
        weapon_Trans.localPosition = Vector3.Lerp(weapon_Trans.localPosition, weapon_origin, Time.deltaTime * time);
        shoot_Trans.localPosition = Vector3.Lerp(shoot_Trans.localPosition, shoot_origin, Time.deltaTime * time);
    }
    #endregion

    #region - Methods -
    /// <summary>
    /// 受傷
    /// </summary>
    /// <param name="damage">傷害值</param>s
    public void TakeDamage(int damage)
    {
        if (photonView.IsMine)
        {
            currentHealth -= damage;

            if (currentHealth <= 0)
            {
                Die();
            }
        }
    }

    ///// <summary>
    ///// 鼠標消失
    ///// </summary>
    //void CursorLock()
    //{
    //    //if (cursorLocked)
    //    //{
    //    Cursor.lockState = CursorLockMode.Locked;
    //    //    Cursor.visible = false;

    //    //    if (Input.GetKeyDown(KeyCode.Escape))
    //    //    {
    //    //        cursorLocked = false;
    //    //    }
    //    //}
    //    //else
    //    //{
    //    //    Cursor.lockState = CursorLockMode.None;
    //    //    Cursor.visible = true;

    //    //    if (Input.GetKeyDown(KeyCode.Escape))
    //    //    {
    //    //        cursorLocked = true;
    //    //    }
    //    //}
    //}

    /// <summary>
    /// 視角
    /// </summary>
    void View()
    {
        // 角色直接旋轉 (左右)
        transform.Rotate(Vector3.up * Input.GetAxisRaw("Mouse X") * mouseSensitivity_X * Time.deltaTime * 60f);

        lookRotation += Input.GetAxisRaw("Mouse Y") * mouseSensitivity_Y * Time.deltaTime * 60f;
        lookRotation = Mathf.Clamp(lookRotation, -80, 75);

        // 攝影機角度轉換 (上下)
        cameraHolder.transform.localEulerAngles = -Vector3.right * lookRotation;

        // 讓武器同步轉角度
        weapon_Trans.rotation = cameraHolder.transform.rotation;

        // 讓子彈同步轉角度
        shoot_Trans.rotation = cameraHolder.transform.rotation;
    }

    /// <summary>
    /// 所有移動
    /// </summary>
    void Movement()
    {
        // 一般移動
        if (isMoving && !isSliding && !isCrouching)
        {
            rig.MovePosition(rig.position + transform.TransformDirection(moveDir) * Time.deltaTime);
        }

        // 蹲下狀態
        else if (isCrouching)
        {
            rig.MovePosition(rig.position + transform.TransformDirection(moveDir) * Time.deltaTime);
        }

        // 滑行狀態
        else if (isSliding)
        {
            // 移動
            rig.MovePosition(rig.position + transform.TransformDirection(moveDir) * Time.deltaTime);

            slideTime -= Time.deltaTime;

            if (slideTime <= 0 && isGrounded)
            {
                isSliding = false;
            }
        }
    }

    /// <summary>
    /// 正常移動
    /// </summary>
    void Move()
    {
        // 偵測鍵盤 H : AD | V : WS
        direction = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical")).normalized;

        // 判斷是否移動中
        isMoving = direction != Vector3.zero;

        // 判斷是否在跑步
        isRunning = Input.GetKey(KeyCode.W) && currentSpeed > 10.5f && !scr_weapon.isAim;

        // 跑步中調整 FOV
        if (isRunning) playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, runFOV, Time.deltaTime * 5f);
        else playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, walkFOV, Time.deltaTime * 5f); ;

        // 滑順移動
        moveDir = Vector3.SmoothDamp(moveDir, direction * currentSpeed, ref moveSmoothVelocity, moveSmoothTime);
    }

    /// <summary>
    /// 滑行
    /// </summary>
    void Slide()
    {
        bool slide = Input.GetKey(KeyCode.LeftShift);
        isSliding = slide && isRunning && (slideTime >= 0 || !isGrounded);

        if (isSliding)
        {
            photonView.RPC("ChangePosition", RpcTarget.All, 1.2f, 8f);
        }
        else
        {
            photonView.RPC("ResetPosition", RpcTarget.All, 8f);
        }

        if (Input.GetKeyUp(KeyCode.LeftShift))
        {
            slideTime = slide_time;
            photonView.RPC("ResetPosition", RpcTarget.All, 8f);
        }
    }

    /// <summary>
    /// 蹲下
    /// </summary>
    void Crounch()
    {
        bool crouch = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        isCrouching = crouch && !isRunning && isGrounded;

        if (isCrouching)
        {
            photonView.RPC("ChangePosition", RpcTarget.All, 0.8f, 8f);
            //standCollider.SetActive(false);
            //  crounchCollider.SetActive(true);
        }
        else
        {
            photonView.RPC("ResetPosition", RpcTarget.All, 8f);
            //  standCollider.SetActive(true);
            //  crounchCollider.SetActive(false);
        }
    }

    /// <summary>
    /// 跳躍
    /// </summary>0
    void Jump()
    {
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rig.AddForce(transform.up * jumpForce);
        }
    }

    /// <summary>
    /// 呼吸
    /// </summary>
    /// <param name="p_x">X 倍率</param>
    /// <param name="p_y">Y 倍率</param>
    void Breath(float p_x, float p_y)
    {
        Vector3 temp = new Vector3(Mathf.Cos(counter) * p_x, Mathf.Sin(counter * 2) * p_y, 0);
        target_weapon_Trans = temp + weapon_Trans.localPosition;
    }

    /// <summary>
    /// 呼吸搖擺切換
    /// </summary>
    void BreathSwitch()
    {
        direction = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical"));

        if (scr_weapon.isAim)
        {
            Breath(0.01f, 0.01f);
            counter += Time.deltaTime;
            weapon_Trans.localPosition = Vector3.Lerp(weapon_Trans.localPosition, target_weapon_Trans, Time.deltaTime);
        }
        else if (isCrouching || isSliding)
        {
            Breath(0.03f, 0.03f);
            counter += Time.deltaTime;
            weapon_Trans.localPosition = Vector3.Lerp(weapon_Trans.localPosition, target_weapon_Trans, Time.deltaTime * 2f);
        }
        else if (direction == Vector3.zero)
        {
            Breath(0.05f, 0.05f);
            counter += Time.deltaTime;
            weapon_Trans.localPosition = Vector3.Lerp(weapon_Trans.localPosition, target_weapon_Trans, Time.deltaTime * 2f);
        }
        else if (isRunning)
        {
            Breath(0.1f, 0.1f);
            counter += Time.deltaTime * 8f;
            weapon_Trans.localPosition = Vector3.Lerp(weapon_Trans.localPosition, target_weapon_Trans, Time.deltaTime * 4f);
        }
        else if (isMoving)
        {
            Breath(0.07f, 0.07f);
            counter += Time.deltaTime * 6f;
            weapon_Trans.localPosition = Vector3.Lerp(weapon_Trans.localPosition, target_weapon_Trans, Time.deltaTime * 3f);
        }
    }

    /// <summary>
    /// 死亡
    /// </summary>
    void Die()
    {
        scr_gameManager.Spawn();
        PhotonNetwork.Destroy(gameObject);
    }

    /// <summary>
    /// 更新血條資訊
    /// </summary>
    void UpdateHpBar()
    {
        healthBar.fillAmount = (float)currentHealth / maxHealth;
    }

    /// <summary>
    /// 更新子彈UI
    /// </summary>
    void UpdateAmmo()
    {
        ammo_UI.text = scr_weapon.UpdateAmmo();
    }

    /// <summary>
    /// 計算角色速度
    /// </summary>
    void CalculateSpeed()
    {
        // 瞄準 : 速度最慢
        if (scr_weapon.isAim)
        {
            currentSpeed = Mathf.Lerp(currentSpeed, aimSpeed, Time.deltaTime * 30f);
        }
        // 滑行 : 速度最快
        else if (isSliding)
        {
            currentSpeed = Mathf.Lerp(currentSpeed, slideSpeed, Time.deltaTime * 20f);
        }
        // 蹲下 : 速度第二慢
        else if (isCrouching)
        {
            currentSpeed = Mathf.Lerp(currentSpeed, crouchSpeed, Time.deltaTime * 20f);
        }
        // 移動 : 速度漸快
        else if (isMoving)
        {
            if (direction.x == 0 && direction.z >= 0)
            {
                currentSpeed = Mathf.Lerp(currentSpeed, runSpeed, Time.deltaTime * 1.5f);
            }
            else if (direction.x != 0 && direction.z >= 0)
            {
                currentSpeed = Mathf.Lerp(currentSpeed, runSpeed - 1.5f, Time.deltaTime * 1.5f);
            }
            else if (direction.z < 0)
            {
                currentSpeed = Mathf.Lerp(currentSpeed, walkSpeed, Time.deltaTime * 1.5f);
            }
        }
        // 回歸初始速度
        else
        {
            currentSpeed = Mathf.Lerp(currentSpeed, walkSpeed, Time.deltaTime * 5f);
        }
    }

    /// <summary>
    /// 暫停
    /// </summary>
    void Pause()
    {
        paused = Input.GetKeyDown(KeyCode.Escape);

        if (paused)
        {
            GameObject.Find("畫布").GetComponent<scr_SceneManager>().Pause();
            moveDir = Vector3.zero;
        }
    }
    #endregion
}
