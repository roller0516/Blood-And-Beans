using UnityEngine;

[RequireComponent(typeof(ItemBox), typeof(Renderer))]
public class ItemBoxView : MonoBehaviour
{
    ItemBox box;
    Renderer view;

    void Awake()
    {
        box = GetComponent<ItemBox>();
        view = GetComponent<Renderer>();
    }

    void Update()
    {
        if (box != null && view != null) view.enabled = box.Cleared;
    }
}
