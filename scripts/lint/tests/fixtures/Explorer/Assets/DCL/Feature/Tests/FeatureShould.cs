using UnityEngine;

public class FeatureShould
{
    public async void TestBad() { }

    public async Task TestGood() { }

    public void NonProdExclusionsHoldHere()
    {
        Debug.Log("allowed in tests");
        var proxy = new ObjectProxy<int>();
        var cam = Camera.main;
    }
}
