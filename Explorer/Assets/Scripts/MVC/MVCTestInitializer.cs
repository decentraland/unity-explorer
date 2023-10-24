using Cysharp.Threading.Tasks;
using MVC;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

public class MVCTestInitializer : MonoBehaviour
{
    [SerializeField] private Button spanwPopup1;
    [SerializeField] private Button spanwPopup2;
    [SerializeField] private Button spanwFullscreen;
    [SerializeField] private Button spanwPersistent;
    [SerializeField] private Button spanwTop;
    // Start is called before the first frame update
    private MVCManager mvcManager;
    public void Start()
    {
        PopupCloserView popupCloserView = Instantiate(Resources.Load<PopupCloserView>("PopupCloser"), Vector3.zero, Quaternion.identity, null);
        mvcManager = new MVCManager(new WindowStackManager(), new CancellationTokenSource(), popupCloserView);
        TestPopupView popupView = Resources.Load<TestPopupView>("SamplePopup");
        mvcManager.RegisterController(new TestPopupController(TestPopupController.CreateLazily(popupView, null)));

        TestPopup2View popup2View = Resources.Load<TestPopup2View>("SamplePopup2");
        mvcManager.RegisterController(new TestPopup2Controller(TestPopup2Controller.CreateLazily(popup2View, null)));

        TestFullscreenView fullscreenView = Resources.Load<TestFullscreenView>("SampleFullscreen");
        mvcManager.RegisterController(new TestFullscreenController(TestFullscreenController.CreateLazily(fullscreenView, null)));

        TestPersistentView persistentView = Resources.Load<TestPersistentView>("SamplePersistent");
        mvcManager.RegisterController(new TestPersistentController(TestPersistentController.CreateLazily(persistentView, null)));

        TestTopView topView = Resources.Load<TestTopView>("SampleTop");
        mvcManager.RegisterController(new TestTopController(TestTopController.CreateLazily(topView, null)));

        spanwPopup1.onClick.AddListener(()=>mvcManager.Show(TestPopupController.IssueCommand(new MVCCheetSheet.ExampleParam("TEST"))).Forget());
        spanwPopup2.onClick.AddListener(()=>mvcManager.Show(TestPopup2Controller.IssueCommand(new MVCCheetSheet.ExampleParam("TEST"))).Forget());
        spanwFullscreen.onClick.AddListener(()=>mvcManager.Show(TestFullscreenController.IssueCommand(new MVCCheetSheet.ExampleParam("TEST"))).Forget());
        spanwPersistent.onClick.AddListener(()=>mvcManager.Show(TestPersistentController.IssueCommand(new MVCCheetSheet.ExampleParam("TEST"))).Forget());
        spanwTop.onClick.AddListener(()=>mvcManager.Show(TestTopController.IssueCommand(new MVCCheetSheet.ExampleParam("TEST"))).Forget());
    }
}
