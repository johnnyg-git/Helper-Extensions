using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Recognition;
using System.Text;
using System.Threading.Tasks;
using Helper_Reborn;
using ThunderRoad;
using UnityEngine;
using Newtonsoft.Json;

namespace Helper_Extensions
{
    class Gravity : commandHandler
    {
        EffectData bubbleEffectData;
        Trigger captureTrigger;
        bool bubbleActive;

        public float liftMinForce = 0.3f;
        public float liftMaxForce = 0.6f;
        public float liftRagdollForceMultiplier = 2f;
        public float bubbleEffectMaxScale = 15f;
        public float liftDrag = 1f;
        static Vector3[] randomTorqueArray;
        public Vector3 randomTorqueRange = new Vector3(0.2f, 0.2f, 0.2f);
        List<CollisionHandler> capturedObjects = new List<CollisionHandler>();
        public AnimationCurve bubbleScaleCurveOverTime;

        IEnumerator Bubble(float duration)
        {
            randomTorqueArray = new Vector3[5];
            randomTorqueArray[0] = new Vector3(UnityEngine.Random.Range(-randomTorqueRange.x, randomTorqueRange.x), UnityEngine.Random.Range(-randomTorqueRange.y, randomTorqueRange.y), UnityEngine.Random.Range(-randomTorqueRange.z, randomTorqueRange.z));
            randomTorqueArray[1] = new Vector3(UnityEngine.Random.Range(-randomTorqueRange.x, randomTorqueRange.x), UnityEngine.Random.Range(-randomTorqueRange.y, randomTorqueRange.y), UnityEngine.Random.Range(-randomTorqueRange.z, randomTorqueRange.z));
            randomTorqueArray[2] = new Vector3(UnityEngine.Random.Range(-randomTorqueRange.x, randomTorqueRange.x), UnityEngine.Random.Range(-randomTorqueRange.y, randomTorqueRange.y), UnityEngine.Random.Range(-randomTorqueRange.z, randomTorqueRange.z));
            randomTorqueArray[3] = new Vector3(UnityEngine.Random.Range(-randomTorqueRange.x, randomTorqueRange.x), UnityEngine.Random.Range(-randomTorqueRange.y, randomTorqueRange.y), UnityEngine.Random.Range(-randomTorqueRange.z, randomTorqueRange.z));
            randomTorqueArray[4] = new Vector3(UnityEngine.Random.Range(-randomTorqueRange.x, randomTorqueRange.x), UnityEngine.Random.Range(-randomTorqueRange.y, randomTorqueRange.y), UnityEngine.Random.Range(-randomTorqueRange.z, randomTorqueRange.z));
            bubbleActive = true;
            EffectInstance bubbleEffect = null;
            StopCapture();
            if (bubbleEffectData != null)
            {
                bubbleEffect = bubbleEffectData.Spawn(Creature.player.transform.position, Quaternion.identity);
                bubbleEffect.SetIntensity(0f);
                bubbleEffect.Play(0);
            }
            yield return new WaitForFixedUpdate();
            StartCapture(0f);
            float startTime = Time.time;
            while (Time.time - startTime < duration)
            {
                float num = bubbleScaleCurveOverTime.Evaluate((Time.time - startTime) / duration);
                captureTrigger.SetRadius(num * bubbleEffectMaxScale * 0.5f);
                if (bubbleEffect != null)
                {
                    bubbleEffect.SetIntensity(num);
                }
                yield return null;
            }
            if (bubbleEffect != null)
            {
                bubbleEffect.End(false, -1f);
            }
            StopCapture();
            bubbleActive = false;
            yield break;
        }

        public void StartCapture(float radius)
        {
            captureTrigger = new GameObject("GravityTrigger").AddComponent<Trigger>();
            captureTrigger.transform.localPosition = Vector3.zero;
            captureTrigger.transform.localRotation = Quaternion.identity;
            captureTrigger.SetCallback(new Trigger.CallBack(OnTrigger));
            captureTrigger.SetLayer(GameManager.GetLayer(LayerName.MovingObject));
            captureTrigger.SetRadius(radius);
            captureTrigger.SetActive(true);
        }

        void OnTrigger(Collider other, bool enter)
        {
            if (other.attachedRigidbody && !other.attachedRigidbody.isKinematic)
            {
                CollisionHandler component = other.attachedRigidbody.GetComponent<CollisionHandler>();
                if (component && (!component.item || component.item.data.type != ItemPhysic.Type.Body))
                {
                    if (enter)
                    {
                        if (component.item || (bubbleActive && component.ragdollPart && component.ragdollPart.ragdoll != Creature.player))
                        {
                            if (component.ragdollPart && (component.ragdollPart.ragdoll.state == Creature.State.Alive || component.ragdollPart.ragdoll.standingUp))
                            {
                                component.ragdollPart.ragdoll.SetState(Creature.State.Destabilized);
                                component.ragdollPart.ragdoll.AddNoStandUpModifier(this);
                            }
                            component.SetPhysicModifier(this, 2, 0f, 1f, liftDrag, -1f, null);
                            Vector3 vector = -Physics.gravity.normalized * Mathf.Lerp(liftMinForce, liftMaxForce, UnityEngine.Random.Range(0f, 1f));
                            if (component.ragdollPart)
                            {
                                vector *= liftRagdollForceMultiplier;
                            }
                            component.rb.AddForce(vector, ForceMode.VelocityChange);
                            component.rb.AddTorque(randomTorqueArray[UnityEngine.Random.Range(0, 5)], ForceMode.VelocityChange);
                            capturedObjects.Add(component);
                            return;
                        }
                    }
                    else
                    {
                        component.RemovePhysicModifier(this);
                        if (component.ragdollPart && component.ragdollPart.ragdoll != Creature.player.ragdoll)
                        {
                            component.ragdollPart.ragdoll.RemoveNoStandUpModifier(this);
                        }
                        capturedObjects.Remove(component);
                    }
                }
            }
        }

        public void StopCapture()
        {
            captureTrigger.SetActive(false);
            for (int i = capturedObjects.Count - 1; i >= 0; i--)
            {
                capturedObjects[i].RemovePhysicModifier(this);
                if (capturedObjects[i].ragdollPart && capturedObjects[i].ragdollPart.ragdoll != Creature.player.ragdoll)
                {
                    capturedObjects[i].ragdollPart.ragdoll.RemoveNoStandUpModifier(this);
                }
                capturedObjects.RemoveAt(i);
            }
            UnityEngine.Object.Destroy(captureTrigger.gameObject);
        }



        public override void setupGrammar(SpeechRecognitionEngine recognitionEngine)
        {
            base.setupGrammar(recognitionEngine);

            GrammarBuilder builder = new GrammarBuilder(helperModule.instance.activationWord);
            Choices commandWords = new Choices(commandPhrases);
            builder.Append(new SemanticResultKey("cmd", commandWords));

            Grammar cmd = new Grammar(builder);
            cmd.Name = "gravityCmd";
            recognitionEngine.LoadGrammar(cmd);
            bubbleEffectData = Catalog.GetData<EffectData>("SpellGravityBubble");

            bubbleScaleCurveOverTime = Catalog.GetData<SpellMergeGravity>("GravityMerge").bubbleScaleCurveOverTime;
        }

        public override void doCommand(RecognitionResult speechResult)
        {
            base.doCommand(speechResult);
            if (Creature.player == null) return;
            
        }
    }

    class Healer : commandHandler
    {
        public override void setupGrammar(SpeechRecognitionEngine recognitionEngine)
        {
            base.setupGrammar(recognitionEngine);

            GrammarBuilder builder = new GrammarBuilder(helperModule.instance.activationWord);
            Choices commandWords = new Choices(commandPhrases);
            builder.Append(new SemanticResultKey("cmd", commandWords));
            Choices numbers = new Choices(" ", "");
            for (int i = 1; i <= 100; i++)
                numbers.Add(i.ToString());
            builder.Append(new SemanticResultKey("number", numbers));

            Grammar cmd = new Grammar(builder);
            cmd.Name = "healerCmd";
            recognitionEngine.LoadGrammar(cmd);
        }

        public override void doCommand(RecognitionResult speechResult)
        {
            base.doCommand(speechResult);
            if (Creature.player == null) return;
            Creature.player.health.Heal(Int32.Parse(speechResult.Semantics["number"].Value.ToString()), Creature.player);
        }
    }

    class WarCrimes : commandHandler
    {
        public override void setupGrammar(SpeechRecognitionEngine recognitionEngine)
        {
            base.setupGrammar(recognitionEngine);

            GrammarBuilder builder = new GrammarBuilder(helperModule.instance.activationWord);
            Choices commandWords = new Choices(commandPhrases);
            builder.Append(new SemanticResultKey("cmd", commandWords));
            Choices numbers = new Choices(" ", "");

            Grammar cmd = new Grammar(builder);
            cmd.Name = "warcrimes";
            recognitionEngine.LoadGrammar(cmd);
        }

        public override void doCommand(RecognitionResult speechResult)
        {
            base.doCommand(speechResult);
            foreach(Creature c in Creature.list)
            {
                if (c != Creature.player) c.health.Kill();
            }
        }
    }

    class GenivaConvention : commandHandler
    {
        public override void setupGrammar(SpeechRecognitionEngine recognitionEngine)
        {
            base.setupGrammar(recognitionEngine);

            GrammarBuilder builder = new GrammarBuilder(helperModule.instance.activationWord);
            Choices commandWords = new Choices(commandPhrases);
            builder.Append(new SemanticResultKey("cmd", commandWords));
            Choices numbers = new Choices(" ", "");

            Grammar cmd = new Grammar(builder);
            cmd.Name = "genivaconvention";
            recognitionEngine.LoadGrammar(cmd);
        }

        public override void doCommand(RecognitionResult speechResult)
        {
            base.doCommand(speechResult);
            foreach (Creature c in Creature.list)
            {
                if (c != Creature.player) c.health.Resurrect();
            }
        }
    }

    class Electrocution : commandHandler
    {
        public override void setupGrammar(SpeechRecognitionEngine recognitionEngine)
        {
            base.setupGrammar(recognitionEngine);

            GrammarBuilder builder = new GrammarBuilder(helperModule.instance.activationWord);
            Choices commandWords = new Choices(commandPhrases);
            builder.Append(new SemanticResultKey("cmd", commandWords));

            Grammar cmd = new Grammar(builder);
            cmd.Name = "electrocutionCmd";
            recognitionEngine.LoadGrammar(cmd);
        }

        public override void doCommand(RecognitionResult speechResult)
        {
            base.doCommand(speechResult);
            foreach(Creature c in Creature.list)
            {
                if (c == Creature.player) continue;
                ActionShock actionShock = c.GetAction<ActionShock>();
                if (actionShock != null)
                {
                    actionShock.Refresh(1f, 1f);
                }
                else
                {
                    actionShock = new ActionShock(1, 2f, Catalog.GetData<EffectData>("ImbueLightningRagdoll"));
                    c.TryAction(actionShock, true);
                }
            }
        }
    }
}
