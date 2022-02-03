using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace ShowProjectPathOnUnityWindowTitle
{
    /// <summary>
    /// Unity Editorのウィンドウタイトルに上位フォルダ名を表示する。
    /// Unity Editorのウィンドウを複数開いたときに区別できるようにするため。
    /// ReflectionでUnityのinternalなAPIを利用している。
    /// 
    /// 参考: https://qiita.com/mob-sakai/items/f3bbc0c45abc31ea7ac0
    /// </summary>
    [InitializeOnLoad]
    public class ShowProjectPathOnUnityWindowTitle
    {
        static ShowProjectPathOnUnityWindowTitle()
        {
            // 1フレーム後に UpdateWindowTitle() を実行するための仕掛け
            EditorApplication.update += EditorUpdate;
            RequestUpdate();
        }
        
        private static Type EditorApplicationType => typeof(EditorApplication);
        private static Type ApplicationTitleDescriptorType => GetApplicationTitleDescriptorType();
        
        // EditorApplication.updateMainWindowTitle event のリファレンス
        // 定義: internal static event Action<ApplicationTitleDescriptor> updateMainWindowTitle;
        private static EventInfo UpdateMainWindowTitleEventInfo => 
            EditorApplicationType.GetEvent("updateMainWindowTitle", BindingFlags.Static | BindingFlags.NonPublic);

        // EditorApplication.UpdateMainWindowTitle() メソッドのリファレンス（名前がほぼ同じだが上記eventとは別もの）
        // 定義: internal static extern void UpdateMainWindowTitle();
        private static MethodInfo UpdateMainWindowTitleMethodInfo =>
            EditorApplicationType.GetMethod("UpdateMainWindowTitle", BindingFlags.Static | BindingFlags.NonPublic);

        static Type GetApplicationTitleDescriptorType()
        {
            return EditorApplicationType.Assembly.GetTypes()
                .First(x => x.FullName == "UnityEditor.ApplicationTitleDescriptor");
        }

        private static bool updateRequested = false;

        [DidReloadScripts]
        static void RequestUpdate()
        {
            updateRequested = true;
        }
        
        static void EditorUpdate()
        {
            if (updateRequested)
            {
                updateRequested = false;
                UpdateWindowTitle();
            }
        }
        
        // Unity Editorウィンドウタイトルを更新する
        static void UpdateWindowTitle()
        {
            // Action<object>をAction<ApplicationTitleDescriptor>に変換
            Type delegateType = typeof(Action<>).MakeGenericType(ApplicationTitleDescriptorType);
            MethodInfo methodInfo = ((Action<object>)OnUpdateMainWindowTitle).Method;
            Delegate del = Delegate.CreateDelegate(delegateType, null, methodInfo);

            // UpdateMainWindowTitleを呼び出す前後にイベントの追加/削除
            // 以下の内容をReflectionで書いている。
            // EditorApplication.updateMainWindowTitle += cb;
            // EditorApplication.UpdateMainWindowTitle();
            // EditorApplication.updateMainWindowTitle -= cb;
            UpdateMainWindowTitleEventInfo?.GetAddMethod(true).Invoke(null, new object[] { del });
            UpdateMainWindowTitleMethodInfo?.Invoke(null, Array.Empty<object>());
            UpdateMainWindowTitleEventInfo?.GetRemoveMethod(true).Invoke(null, new object[] { del });
        }

        /// <summary>
        /// UnityEditorウィンドウタイトルを更新するためのハンドラ
        /// 受け取ったdescのtitleを変更することでウィンドウタイトルに反映される
        /// </summary>
        /// <param name="desc">実際には 'ApplicationTitleDescriptor' type (internal型)</param>
        static void OnUpdateMainWindowTitle(object desc)
        {
            // desc.title を取得
            FieldInfo field = desc.GetType().GetField("title");
            var title = field.GetValue(desc) as string;
            
            // タイトル文字列を編集して desc.title にセット
            var newTitle = TranslateWindowTitle(title);
            var titleFieldInfo = ApplicationTitleDescriptorType
                .GetField("title", BindingFlags.Instance | BindingFlags.Public);
            titleFieldInfo?.SetValue(desc, newTitle);
        }

        /// <summary>
        /// ウィンドウタイトル文字列を生成する。
        /// 元のタイトル文字列を受け取って、修正して返す
        /// </summary>
        /// <param name="currentTitle">修正前のウィンドウタイトル文字列</param>
        /// <returns>修正後のウィンドウタイトル文字列を返す</returns>
        static string TranslateWindowTitle(string currentTitle)
        {
            return currentTitle + " (at " + GetShortProjectPathString() + ")";
        }

        /// <summary>
        /// タイトルバーに表示したいパス情報を生成
        /// プロジェクトフォルダの親の親からプロジェクトフォルダまでのパスを返す
        /// </summary>
        /// <returns></returns>
        static string GetShortProjectPathString()
        {
            var dirInfo = new DirectoryInfo(Application.dataPath);
            string pathString = dirInfo.Parent?.Parent?.Parent?.Name + "\\" + dirInfo.Parent?.Parent?.Name + "\\" + dirInfo.Parent?.Name;
            return pathString;
        }
    }
}