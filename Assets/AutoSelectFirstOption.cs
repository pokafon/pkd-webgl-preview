using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 選択肢（ボタン）が新しく表示されるたびに、一番上のボタンへ自動でフォーカスを当てるスクリプト。
///
/// 前バージョンの不具合：ボタンの「数」で変化を判定していたため、
/// 前回と同じ数の選択肢（例：2択→2択）が続くと変化なしと判定され、
/// 2回目以降フォーカスが当たらなかった。
///
/// 今回は「一番上のボタンの実体（インスタンス）」が前回と違うかどうかで判定する。
/// Options Presenterは選択肢が表示されるたびにボタンを作り直すため、
/// 数が同じでも実体は必ず変わる。
/// </summary>
public class AutoSelectFirstOption : MonoBehaviour
{
    private Selectable lastFirst = null;

    void Update()
    {
        Selectable[] selectables = GetComponentsInChildren<Selectable>(false);

        Selectable first = null;
        foreach (var s in selectables)
        {
            if (s.gameObject.activeInHierarchy && s.interactable)
            {
                first = s;
                break;
            }
        }

        if (first != null && first != lastFirst)
        {
            lastFirst = first;
            EventSystem.current.SetSelectedGameObject(first.gameObject);
        }
        else if (first == null)
        {
            lastFirst = null;
        }
    }
}