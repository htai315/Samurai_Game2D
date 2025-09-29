using System;
using UnityEngine;

public sealed class CharacterEvents : MonoBehaviour
{
    public event Action DashStarted;
    public event Action DashEnded;
    public event Action Hurt;
    public event Action Died;
    public event Action Jumped;
    public event Action Landed;

    public void InvokeDashStarted() => DashStarted?.Invoke();
    public void InvokeDashEnded() => DashEnded?.Invoke();
    public void InvokeHurt() => Hurt?.Invoke();
    public void InvokeDied() => Died?.Invoke();
    public void InvokeJumped() => Jumped?.Invoke();
    public void InvokeLanded() => Landed?.Invoke();
}
