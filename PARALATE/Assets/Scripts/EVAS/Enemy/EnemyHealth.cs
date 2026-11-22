using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class EnemyHealth : MonoBehaviour
{
    public enum DeathAction { Destroy, FallOver, Revive }

    [Header("Health Settings")]
    public float maxHealth = 100f;
    private float currentHealth;

    [Header("UI Settings")]
    public Image healthBarFill; // ใส่ Image ของ Health
    public Transform healthCanvas; // ใส่ Canvas ให้หันเข้ากล้อง

    [Header("Death Settings")]
    public DeathAction onDeath;

    [Header("Stealth Takedown")]
    [Tooltip("ต้องกด Ctrl ค้างกี่วินาทีเพื่อรัดคอศัตรูชนิดนี้จนหมดสติ (ศัตรูตัวใหญ่/หัวหน้า ตั้งให้นานขึ้น)")]
    [Min(0.1f)] public float chokeHoldSeconds = 3f;

    private bool isDead = false;
    private bool isStunned = false;
    private Quaternion standingRotation;
    private Coroutine knockOutRoutine;
    private EnemyController enemyController;
    private Camera mainCamera;

    public bool IsDead => isDead;
    public bool IsStunned => isStunned;
    public float HealthPercent => maxHealth <= 0f ? 0f : currentHealth / maxHealth;

    private void Awake()
    {
        enemyController = GetComponent<EnemyController>();
        HeadHitbox.EnsureOn(this);
    }

    /// <summary>ดาเมจจากปืน: isHeadshot = true เมื่อกระสุนผ่าน HeadHitbox (ตัวคูณมาจากปืนแต่ละกระบอก)</summary>
    public void TakeDamage(float amount, bool isHeadshot)
    {
        if (isHeadshot && !isDead)
            SfxLibrary.Play("headshot", transform.position);

        TakeDamage(amount);
    }

    private void Start()
    {
        currentHealth = maxHealth;
        UpdateHealthBar();
    }

    private void LateUpdate()
    {
        if (healthCanvas == null || !healthCanvas.gameObject.activeInHierarchy)
            return;

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null)
            return;

        // Billboard: หมุนให้ตรงกับกล้องพอดี
        healthCanvas.rotation = mainCamera.transform.rotation;
    }

    public void KnockOut(float stunDuration)
    {
        if (isDead || isStunned)
            return;

        if (knockOutRoutine != null)
            StopCoroutine(knockOutRoutine);

        knockOutRoutine = StartCoroutine(KnockOutRoutine(stunDuration));
    }

    private IEnumerator KnockOutRoutine(float stunDuration)
    {
        isStunned = true;
        SetMovementLocked(true);
        standingRotation = transform.rotation;
        transform.rotation = Quaternion.Euler(90f, transform.eulerAngles.y, transform.eulerAngles.z);

        if (healthCanvas != null)
            healthCanvas.gameObject.SetActive(false);

        yield return new WaitForSeconds(stunDuration);

        if (!isDead)
        {
            isStunned = false;
            transform.rotation = standingRotation;
            SetMovementLocked(false);

            if (healthCanvas != null)
                healthCanvas.gameObject.SetActive(true);
        }

        knockOutRoutine = null;
    }

    public void TakeDamage(float amount)
    {
        // ศัตรูที่สลบอยู่ยังรับดาเมจได้ (ยิง/ฟัน/กด F ฆ่าตอนสลบได้) เดิมถูกกันไว้ทำให้ฆ่าไม่ได้เลย
        if (isDead)
            return;

        currentHealth = Mathf.Max(currentHealth - amount, 0f);
        UpdateHealthBar();

        if (currentHealth <= 0f)
            Die();
    }

    private void UpdateHealthBar()
    {
        // fillAmount รับค่า 0-1 เท่านั้น จึงต้องหารด้วยเลือดสูงสุด
        if (healthBarFill != null && maxHealth > 0f)
            healthBarFill.fillAmount = currentHealth / maxHealth;
    }

    /// <summary>ฆ่าทันทีด้วยวิธีตายที่กำหนด (เช่น แทงคอจากด้านหลัง = FallOver ศพนอนบนพื้น)</summary>
    public void Kill(DeathAction action)
    {
        if (isDead)
            return;

        currentHealth = 0f;
        UpdateHealthBar();
        Die(action);
    }

    /// <summary>ถูกรัดคอจนหมดสติ: นับเป็นศัตรูที่ถูกจัดการแล้ว ร่างหายไปจากฉาก</summary>
    public void Vanish()
    {
        if (isDead)
            return;

        isDead = true;
        isStunned = false;

        if (knockOutRoutine != null)
        {
            StopCoroutine(knockOutRoutine);
            knockOutRoutine = null;
        }

        gameObject.SetActive(false);
    }

    private void Die()
    {
        Die(onDeath);
    }

    private void Die(DeathAction action)
    {
        isDead = true;

        // ตายตอนสลบ: หยุดตัวนับเวลาฟื้น ไม่ให้ลุกขึ้นมายืนหลังตาย
        if (knockOutRoutine != null)
        {
            StopCoroutine(knockOutRoutine);
            knockOutRoutine = null;
        }

        bool wasStunned = isStunned;
        isStunned = false;
        SetMovementLocked(true);

        switch (action)
        {
            case DeathAction.Destroy:
                Destroy(gameObject);
                break;

            case DeathAction.FallOver:
                // หมุนให้นอนลง 90 องศาแนวแกน X และปิดหลอดเลือด
                transform.rotation = Quaternion.Euler(90f, transform.eulerAngles.y, transform.eulerAngles.z);

                if (healthCanvas != null)
                    healthCanvas.gameObject.SetActive(false);
                break;

            case DeathAction.Revive:
                // ชุบชีวิต กลับมาเลือดเต็ม
                isDead = false;
                if (wasStunned)
                    transform.rotation = standingRotation;
                if (healthCanvas != null)
                    healthCanvas.gameObject.SetActive(true);
                SetMovementLocked(false);
                currentHealth = maxHealth;
                UpdateHealthBar();
                break;
        }
    }

    private void SetMovementLocked(bool locked)
    {
        if (enemyController == null)
            enemyController = GetComponent<EnemyController>();

        if (enemyController != null)
            enemyController.SetMovementLocked(this, locked);
    }
}
