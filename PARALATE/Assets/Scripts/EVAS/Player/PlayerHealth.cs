using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour
{
    public float health;
    public float maxHealth;
    public Image healthBar;
    public Image DeadScene;

    [Header("Debug")]
    public bool isInvincible;

    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private bool isDead;
    private CharacterController characterController;

    /// <summary>ผู้เล่นตายอยู่ตอนนี้ไหม (ศัตรูใช้เช็กเพื่อหยุดยิง/หยุดไล่)</summary>
    public static bool PlayerIsDead { get; private set; }

    /// <summary>เรียกครั้งเดียวตอนผู้เล่นตาย</summary>
    public static event System.Action Died;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        PlayerIsDead = false;
        Died = null;
    }
    private TopDownPlayerController movementController;

    void Start()
    {
        PlayerIsDead = false; // เริ่ม Scene ใหม่ / โหลดใหม่หลังตาย

        // เคารพค่า Max Health ที่ตั้งใน Inspector (เดิมถูกเขียนทับด้วย health เสมอ)
        if (maxHealth <= 0f)
            maxHealth = health;

        if (health <= 0f || health > maxHealth)
            health = maxHealth;
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
        characterController = GetComponent<CharacterController>();
        movementController = GetComponent<TopDownPlayerController>();

        if (DeadScene != null)
            DeadScene.gameObject.SetActive(false);
    }

    void Update()
    {
        UpdateHealthBar();

        if (!isDead && health <= 0)
            Die();
    }

    public void TakeDamage(float damage)
    {
        if (isDead || isInvincible)
            return;

        health = Mathf.Max(health - damage, 0f);
        UpdateHealthBar();
        SfxLibrary.Play("player_hurt");

        if (health <= 0f)
            Die();
    }

    public bool IsDead => isDead;
    public bool IsFull => health >= maxHealth;

    /// <summary>เพิ่มเลือด (ไม่เกิน maxHealth) คืน false ถ้าตายอยู่หรือเลือดเต็มแล้ว (ไอเทมจะไม่ถูกใช้)</summary>
    public bool Heal(float amount)
    {
        if (isDead || amount <= 0f || health >= maxHealth)
            return false;

        health = Mathf.Min(health + amount, maxHealth);
        UpdateHealthBar();
        return true;
    }

    /// <summary>ตั้งเลือดตรงๆ (ใช้ตอนโหลดเซฟ)</summary>
    public void SetHealth(float value)
    {
        if (isDead)
            return;

        health = Mathf.Clamp(value, 1f, maxHealth > 0f ? maxHealth : value);
        UpdateHealthBar();
    }

    public void Respawn()
    {
        isDead = false;
        PlayerIsDead = false;
        health = maxHealth;

        if (characterController != null)
            characterController.enabled = false;

        transform.SetPositionAndRotation(spawnPosition, spawnRotation);

        if (characterController != null)
            characterController.enabled = true;

        if (movementController != null)
            movementController.SetMovementLocked(this, false);

        if (DeadScene != null)
            DeadScene.gameObject.SetActive(false);

        UpdateHealthBar();
    }

    private void Die()
    {
        isDead = true;
        PlayerIsDead = true;
        health = 0f;
        Died?.Invoke();

        if (movementController != null)
            movementController.SetMovementLocked(this, true);

        if (DeadScene != null)
            DeadScene.gameObject.SetActive(true);

        // หน้า Game Over ต้องคลิกปุ่มได้: คืนเมาส์ให้ (โหลดใหม่แล้ว TopDownPlayerController.Start ซ่อนอีกครั้ง)
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // รอบนี้ล้มเหลว: ของที่เก็บมาในรอบนี้ไม่ถูกเก็บถาวร และเล่นเสียงแพ้
        if (LevelManager.Instance != null)
            LevelManager.Instance.OnRunFailed();

        UpdateHealthBar();
    }

    private void UpdateHealthBar()
    {
        if (healthBar == null || maxHealth <= 0f)
            return;

        healthBar.fillAmount = Mathf.Clamp01(health / maxHealth);
    }
}
