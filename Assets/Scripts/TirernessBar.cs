using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TirernessBar : MonoBehaviour
{
	public Slider slider;
	
	public void SetMaxTirerness(float tirerness)
	{
		slider.maxValue = tirerness;
		slider.value = tirerness;
	}

    public void SetTirerness(float tirerness)
	{
		slider.value = tirerness;
	}
}
