using Assets.SimpleLocalization.Scripts;
using UnityEngine;

public class LocalisationHandler : MonoBehaviour
{
	public void Awake()
	{
		LocalizationManager.Read();

		switch (Application.systemLanguage)
		{
			//case SystemLanguage.German:
			//	LocalizationManager.Language = "German";
			//	break;
			case SystemLanguage.Russian:
				LocalizationManager.Language = "Russian";
				break;
			default:
				LocalizationManager.Language = "English";
				break;
		}

		// This way you can localize and format strings from code.
	}
	public void SetLanguage(string language)
    {
        LocalizationManager.Language = language;
    }
}
