using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SuperNewRoles.Roles.Ability.CustomButton;

internal interface IButtonEffect
{
    public bool isEffectActive { get; set; }
    public abstract Action OnEffectEnds { get; }
    public abstract float EffectDuration { get; }
    public virtual bool effectCancellable => false;
    public virtual bool IsEffectDurationInfinity => false;
    public virtual float FillUpTime => 0f;
    public virtual bool doAdditionalEffect => true;

    public static readonly Color color = new(0F, 0.8F, 0F);
    float EffectTimer { get; set; }

    public void OnClick(ActionButton actionButton)
    {
        if (!this.isEffectActive)
        {
            this.isEffectActive = true;
            this.EffectTimer = this.EffectDuration;
            actionButton.cooldownTimerText.color = color;
            Logger.Info($"[IButtonEffect] OnClick: Charge Started. Duration: {this.EffectDuration}");
        }
    }
    public virtual void OnCancel(ActionButton actionButton)
    {
       if (isEffectActive)
        {
            isEffectActive = false;
            // OnEffectEnds() は呼ばない（キャンセルなので）
            Logger.Info("[IButtonEffect] OnCancel: Charge Cancelled.");
        }
    }

    public void OnFixedUpdate(ActionButton actionButton)
    {
        if (isEffectActive)
        {
            // EffectTimerが0より大きい場合のみ減算する
            if (EffectTimer > 0)
            {
                EffectTimer -= Time.deltaTime;
            }

            // EffectTimerが0以下になったら効果終了
            if (EffectTimer <= 0)
            {
                actionButton.cooldownTimerText.color = Palette.EnabledColor;
                if (!IsEffectDurationInfinity || !effectCancellable)
                {
                    isEffectActive = false;
                    OnEffectEnds();
                }
            }
        }

        this.DoEffect(actionButton);

        if (isEffectActive) actionButton.SetCoolDown(EffectTimer, IsEffectDurationInfinity ? 0f : EffectDuration);
    }

    public virtual bool IsEffectAvailable() => true;

    public virtual void DoEffect(ActionButton actionButton, float effectStartTime = 3f)
    {
        //以下はFillup。もし別のeffectにしたくなったらoverrideして自分でなんとかする。
        if (isEffectActive && actionButton.isCoolingDown && EffectTimer < effectStartTime && doAdditionalEffect)
        {
            actionButton.graphic.transform.localPosition = actionButton.position + (Vector3)UnityEngine.Random.insideUnitCircle * 0.05f;
        }
        else
        {
            actionButton.graphic.transform.localPosition = actionButton.position;
        }
    }
}