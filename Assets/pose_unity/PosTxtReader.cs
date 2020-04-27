using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;

// pos.txtのデータ
// https://github.com/miu200521358/3d-pose-baseline-vmd/blob/master/doc/Output.md
// 0 :Hip
// 1 :RHip
// 2 :RKnee
// 3 :RFoot
// 4 :LHip
// 5 :LKnee
// 6 :LFoot
// 7 :Spine
// 8 :Thorax
// 9 :Neck/Nose
// 10:Head
// 11:LShoulder
// 12:LElbow
// 13:LWrist
// 14:RShoulder
// 15:RElbow
// 16:RWrist

public class PosTxtReader : MonoBehaviour
{
	public String posFilename; // pos.txtのファイル名

 	// ----------------------------------------------
	[Header("Option")]
	public int startFrame; // 開始フレーム
	public String endFrame; // 終了フレーム
	public int nowFrame_readonly; // 現在のフレーム (Read only)
	public float upPosition = 0.1f; // 足の沈みの補正値(単位：m)。プラス値で体全体が上へ移動する
	public Boolean showDebugCube; // デバッグ用Cubeの表示フラグ

	// ----------------------------------------------
	[Header("Save Motion")] 
    [Tooltip("When this flag is set, save motion at the end frame of Play.")]
	public Boolean saveMotion; // Playの最終フレーム時にモーションを保存する

    [Tooltip("This is the filename to which the BVH file will be saved. If no filename is given, a new one will be generated based on a timestamp. If the file already exists, a number will be appended.")]
	public String saveBVHFilename; // 保存ファイル名

    [Tooltip("When this flag is set, existing files will be overwritten and no number will be appended at the end to avoid this.")]
 	public bool overwrite = false; // Falseの場合は、上書きせずにファイル名の末尾に数字を付加する。
    
    [Tooltip("When this option is enabled, only humanoid bones will be targeted for detecting bones. This means that things like hair bones will not be added to the list of bones when detecting bones.")]
	public bool enforceHumanoidBones = true; // 髪などの骨格以外のボーンは出力しない

	// ----------------------------------------------
	float scaleRatio = 0.001f;  // pos.txtとUnityモデルのスケール比率
	                             // pos.txtの単位はmmでUnityはmのため、0.001に近い値を指定。モデルの大きさによって調整する
	float headAngle = 15f; // 顔の向きの調整 顔を15度上げる
	// ----------------------------------------------


	float playTime; // 再生時間 
	int frame;	// 再生フレーム
	Transform[] boneT; // モデルのボーンのTransform
	Transform[] cubeT; // デバック表示用のCubeのTransform
	Vector3 rootPosition; // 初期のAvatarの位置
	Quaternion rootRotation; // 初期のAvatarのの回転
	Quaternion[] initRot; // 初期の回転値
	Quaternion[] initInv; // 初期のボーンの方向から計算されるクオータニオンのInverse
	float hipHeight; // hipのposition.y
	List<Vector3[]> pos; // pos.txtのデータを保持するコンテナ
	BVHRecorder recorder; // BVH保存用コンポーネント
	int[] bones = new int[10] { 1, 2, 4, 5, 7, 8, 11, 12, 14, 15 }; // 親ボーン
 	int[] childBones = new int[10] { 2, 3, 5, 6, 8, 10, 12, 13, 15, 16 }; // bonesに対応する子ボーン
 	int boneNum = 17;
	Animator anim;
	int sFrame;
	int eFrame;
	bool bvhSaved = false;

	// pos.txtのデータを読み込み、リストで返す
	List<Vector3[]> ReadPosData(string filename) {
		List<Vector3[]> data = new List<Vector3[]>();

		List<string> lines = new List<string>();
		StreamReader sr = new StreamReader(filename);
		while (!sr.EndOfStream) {
			lines.Add(sr.ReadLine());
		}
		sr.Close();

		try {
			foreach (string line in lines) {
				string line2 = line.Replace(",", "");
				string[] str = line2.Split(new string[] { " " }, System.StringSplitOptions.RemoveEmptyEntries); // スペースで分割し、空の文字列は削除

				Vector3[] vs = new Vector3[boneNum];
				for (int i = 0; i < str.Length; i += 4) {
					vs[(int)(i/4)] = new Vector3(-float.Parse(str[i + 1]), float.Parse(str[i + 3]), -float.Parse(str[i + 2]));
				}
				data.Add(vs);
			}
		} 
		catch (Exception e) {
			Debug.Log("<color=blue>Error! Pos File is broken(" + filename + ").</color>");
			return null;
		}
		return data;
	}

	// BoneTransformの取得。回転の初期値を取得
	void GetInitInfo()
	{
		boneT = new Transform[boneNum];
		initRot = new Quaternion[boneNum];
		initInv = new Quaternion[boneNum];

		boneT[0] = anim.GetBoneTransform(HumanBodyBones.Hips);
		boneT[1] = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
		boneT[2] = anim.GetBoneTransform(HumanBodyBones.RightLowerLeg);
		boneT[3] = anim.GetBoneTransform(HumanBodyBones.RightFoot);
		boneT[4] = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
		boneT[5] = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
		boneT[6] = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
		boneT[7] = anim.GetBoneTransform(HumanBodyBones.Spine);
		boneT[8] = anim.GetBoneTransform(HumanBodyBones.Neck);
		boneT[10] = anim.GetBoneTransform(HumanBodyBones.Head);
		boneT[11] = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
		boneT[12] = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
		boneT[13] = anim.GetBoneTransform(HumanBodyBones.LeftHand);
		boneT[14] = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
		boneT[15] = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
		boneT[16] = anim.GetBoneTransform(HumanBodyBones.RightHand);

		if (boneT[0] == null) {
			Debug.Log("<color=blue>Error! Failed to get Bone Transform. Confirm wherther animation type of your model is Humanoid</color>");
			return;
		}

		// Spine,LHip,RHipで三角形を作ってそれを前方向とする。
		Vector3 initForward = TriangleNormal(boneT[7].position, boneT[4].position, boneT[1].position);
		initInv[0] = Quaternion.Inverse(Quaternion.LookRotation(initForward));

		// initPosition = boneT[0].position;
		rootPosition = this.transform.position;
		rootRotation = this.transform.rotation;
		initRot[0] = boneT[0].rotation;
		hipHeight = boneT[0].position.y - this.transform.position.y;
		for (int i = 0; i < bones.Length; i++) {
			int b = bones[i];
			int cb = childBones[i];

			// 対象モデルの回転の初期値
			initRot[b] = boneT[b].rotation;
			// 初期のボーンの方向から計算されるクオータニオン
			initInv[b] = Quaternion.Inverse(Quaternion.LookRotation(boneT[b].position - boneT[cb].position,initForward));
		}
	}

	// 指定の3点でできる三角形に直交する長さ1のベクトルを返す
	Vector3 TriangleNormal(Vector3 a, Vector3 b, Vector3 c)
	{
		Vector3 d1 = a - b;
		Vector3 d2 = a - c;

		Vector3 dd = Vector3.Cross(d1, d2);
		dd.Normalize();

		return dd;
	}

	// デバック用cubeを生成する。生成済みの場合は位置を更新する
	void UpdateCube(int frame)
	{
		if (cubeT == null) {
			// 初期化して、cubeを生成する
			cubeT = new Transform[boneNum];

			for (int i = 0; i < boneNum; i++) {
				Transform t = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
				t.transform.parent = this.transform;
				t.localPosition = pos[frame][i] * scaleRatio;
				t.name = i.ToString();
				t.localScale = new Vector3(0.05f, 0.05f, 0.05f);
				cubeT[i] = t;

				Destroy(t.GetComponent<BoxCollider>());
			}
		}
		else {
			// モデルと重ならないように少しずらして表示
			Vector3 offset = new Vector3(1.2f, 0, 0);

			// 初期化済みの場合は、cubeの位置を更新する
			for (int i = 0; i < boneNum; i++) {
				cubeT[i].localPosition = pos[frame][i] * scaleRatio + new Vector3(0, upPosition, 0) + offset;
			}
		}
	}

	void Start()
	{

		anim = GetComponent<Animator>();
		playTime = 0;
		if (posFilename == "") {
			Debug.Log("<color=blue>Error! Pos filename  is empty.</color>");
			return;
		}
		if (System.IO.File.Exists(posFilename) == false) {
			Debug.Log("<color=blue>Error! Pos file not found(" + posFilename + ").</color>");
			return;
		}
		pos = ReadPosData(posFilename);
		GetInitInfo();
		if (pos != null) {
			// inspectorで指定した開始フレーム、終了フレーム番号をセット
			if (startFrame >= 0 && startFrame < pos.Count) {
				sFrame = startFrame;
			} else {
				sFrame = 0;
			}
			int ef;
			if (int.TryParse(endFrame, out ef)) {
				if (ef >= sFrame && ef < pos.Count) {
					eFrame = ef;
				} else {
					eFrame = pos.Count - 1;
				}
			} else {
				eFrame = pos.Count - 1;
			}
			frame = sFrame;
		}

		if (saveMotion) {
			recorder = gameObject.AddComponent<BVHRecorder>();
			recorder.scripted = true;
			recorder.targetAvatar = anim;
			recorder.blender = false;
			recorder.enforceHumanoidBones = enforceHumanoidBones;
			recorder.getBones();
			recorder.buildSkeleton();
			recorder.genHierarchy();
			recorder.frameRate = 30.0f;
		}
	}

	void Update()
	{
		if (pos == null || boneT[0] == null) {
			return;
		}
		playTime += Time.deltaTime;

		if (saveMotion && recorder != null) {
			// ファイル出力の場合は1フレームずつ進める
			frame += 1;
		} else {
			frame = sFrame + (int)(playTime * 30.0f);  // pos.txtは30fpsを想定
		}
		if (frame > eFrame) {
			if (saveMotion && recorder != null) {
				if (!bvhSaved) {
					bvhSaved = true;
					if (saveBVHFilename != "") {
						string fullpath = Path.GetFullPath(saveBVHFilename);
						// recorder.directory = Path.GetDirectoryName(fullpath);
						// recorder.filename = Path.GetFileName(fullpath);
						recorder.directory = "";
						recorder.filename = fullpath;
						recorder.overwrite = overwrite;
						recorder.saveBVH();
						Debug.Log("Saved Motion(BVH) to " + recorder.lastSavedFile);
					} else {
						Debug.Log("<color=blue>Error! Save BVH Filename is empty.</color>");
					}
				}
			}
			return;
		}
		nowFrame_readonly = frame; // Inspector表示用

		if (showDebugCube) {
			UpdateCube(frame); // デバッグ用Cubeを表示する
		}

		Vector3[] nowPos = pos[frame];
	
		// センターの移動と回転
		Vector3 posForward = TriangleNormal(nowPos[7], nowPos[4], nowPos[1]);

		this.transform.position = rootRotation * nowPos[0] * scaleRatio + rootPosition + new Vector3(0, upPosition - hipHeight , 0);
		boneT[0].rotation = rootRotation * Quaternion.LookRotation(posForward) * initInv[0] * initRot[0];

		// 各ボーンの回転
		for (int i = 0; i < bones.Length; i++) {
			int b = bones[i];
			int cb = childBones[i];
			boneT[b].rotation = rootRotation * Quaternion.LookRotation(nowPos[b] - nowPos[cb], posForward) * initInv[b] * initRot[b];
		}

		// 顔の向きを上げる調整。両肩を結ぶ線を軸として回転
		boneT[8].rotation = Quaternion.AngleAxis(headAngle, boneT[11].position - boneT[14].position) * boneT[8].rotation;

		if (saveMotion && recorder != null) {
			recorder.captureFrame();
		}
	}
}