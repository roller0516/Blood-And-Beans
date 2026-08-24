public interface IInteractable
{
    string Prompt { get; }
    void BeginInteractionClient();
    void EndInteractionClient();
}
