using Unity.Netcode;

/// v5.0에서 세척은 SharedFacility로 이전했다. 기존 프리팹의 스크립트 참조만 보존한다.
public class Sink : NetworkBehaviour, IInteractable
{
    public string Prompt => string.Empty;
    public void BeginInteractionClient() { }
    public void EndInteractionClient() { }
    [Rpc(SendTo.Server)]
    public void WashHoldBeginRpc(RpcParams p = default) { }
    [Rpc(SendTo.Server)]
    public void WashHoldEndRpc(RpcParams p = default) { }
    [Rpc(SendTo.Server)]
    public void DiscardRpc(RpcParams p = default) { }
}
