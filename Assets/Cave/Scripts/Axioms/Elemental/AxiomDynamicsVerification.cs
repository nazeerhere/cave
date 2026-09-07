namespace Cave.Axioms.Elemental
{
    public struct AxiomDynamicsVerificationResult
    {
        public bool D1ZeroState;
        public bool D2SingleInput;
        public bool D3Relaxation;
        public bool D4CounterResponse;
        public bool D5RapidVsSpaced;
        public bool D6StopAndRestart;
        public bool D7FrenzyInput;
        public bool D8IrregularTimestamps;
        public bool D9LargeGap;
        public bool D10FiniteBounds;
        public bool ProfilesUseSharedEngine;
        public AxiomDynamicResponse Rapid;
        public AxiomDynamicResponse Spaced;

        public bool Passed => D1ZeroState && D2SingleInput && D3Relaxation
            && D4CounterResponse && D5RapidVsSpaced && D6StopAndRestart
            && D7FrenzyInput && D8IrregularTimestamps && D9LargeGap
            && D10FiniteBounds && ProfilesUseSharedEngine;
    }

    /// <summary>Deterministic, Unity-free verification for the bounded shared solver.</summary>
    public static class AxiomDynamicsVerification
    {
        public static AxiomDynamicsVerificationResult VerifyAll()
        {
            AxiomDynamicsVerificationResult result = new AxiomDynamicsVerificationResult();
            result.D1ZeroState = VerifyZeroState();
            result.D2SingleInput = VerifySingleInput();
            result.D3Relaxation = VerifyRelaxation();
            result.D4CounterResponse = VerifyCounterResponse();
            result.D5RapidVsSpaced = VerifyRapidVsSpaced(out result.Rapid, out result.Spaced);
            result.D6StopAndRestart = VerifyStopAndRestart();
            result.D7FrenzyInput = VerifyFrenzyInput();
            result.D8IrregularTimestamps = VerifyIrregularTimestamps();
            result.D9LargeGap = VerifyLargeGap();
            result.D10FiniteBounds = VerifyFiniteBounds();
            result.ProfilesUseSharedEngine = VerifyProfiles();
            return result;
        }

        private static bool VerifyZeroState()
        {
            AxiomDynamicsChannel channel = NewChannel(AxiomKind.Heat);
            AxiomDynamicResponse response = channel.AdvanceTo(10f);
            return Finite(response) && Near(response.Desirable, 0f) && Near(response.Counter, 0f);
        }

        private static bool VerifySingleInput()
        {
            AxiomDynamicsChannel channel = NewChannel(AxiomKind.Heat);
            channel.ApplyInput(1f, 1f, 0f);
            AxiomDynamicResponse response = channel.AdvanceTo(.2f);
            return response.Stack == 1f && response.Desirable > 0f && Finite(response);
        }

        private static bool VerifyRelaxation()
        {
            AxiomDynamicsChannel channel = NewChannel(AxiomKind.Heat);
            channel.ApplyInput(1f, 1f, 0f);
            AxiomDynamicResponse early = channel.AdvanceTo(.1f);
            AxiomDynamicResponse later = channel.AdvanceTo(1.2f);
            return Finite(later) && Near(later.Input, 0f)
                && (!Near(early.Desirable, later.Desirable) || !Near(early.Counter, later.Counter));
        }

        private static bool VerifyCounterResponse()
        {
            AxiomDynamicsChannel channel = NewChannel(AxiomKind.Heat);
            float stack = 0f;
            for (int index = 0; index < 6; index++)
            {
                stack += 1f;
                channel.ApplyInput(stack, 1f, index * .12f);
            }

            AxiomDynamicResponse response = channel.AdvanceTo(1.25f);
            return response.Desirable > 0f && response.Counter > 0f && Finite(response);
        }

        private static bool VerifyRapidVsSpaced(out AxiomDynamicResponse rapid, out AxiomDynamicResponse spaced)
        {
            rapid = ApplyFour(NewChannel(AxiomKind.Heat), .25f);
            spaced = ApplyFour(NewChannel(AxiomKind.Heat), 1.5f);
            return Near(rapid.Stack, 4f) && Near(spaced.Stack, 4f)
                && (!Near(rapid.Desirable, spaced.Desirable, .001f)
                    || !Near(rapid.Counter, spaced.Counter, .001f));
        }

        private static bool VerifyStopAndRestart()
        {
            AxiomDynamicsChannel paused = NewChannel(AxiomKind.Order);
            paused.ApplyInput(1f, 1f, 0f);
            paused.ApplyInput(2f, 1f, .15f);
            paused.ApplyInput(3f, 1f, .30f);
            paused.AdvanceTo(2.0f);
            paused.ApplyInput(4f, 1f, 2.0f);
            AxiomDynamicResponse pausedResponse = paused.AdvanceTo(2.2f);

            AxiomDynamicsChannel continuous = NewChannel(AxiomKind.Order);
            continuous.ApplyInput(1f, 1f, 0f);
            continuous.ApplyInput(2f, 1f, .15f);
            continuous.ApplyInput(3f, 1f, .30f);
            continuous.ApplyInput(4f, 1f, .45f);
            AxiomDynamicResponse continuousResponse = continuous.AdvanceTo(2.2f);
            return !Near(pausedResponse.Desirable, continuousResponse.Desirable, .001f)
                || !Near(pausedResponse.Counter, continuousResponse.Counter, .001f);
        }

        private static bool VerifyFrenzyInput()
        {
            AxiomDynamicsChannel ordinary = NewChannel(AxiomKind.Flow);
            ordinary.ApplyInput(1f, 1f, 0f);
            AxiomDynamicResponse ordinaryResponse = ordinary.AdvanceTo(.2f);
            AxiomDynamicsChannel frenzy = NewChannel(AxiomKind.Flow);
            frenzy.ApplyInput(2f, 2f, 0f);
            AxiomDynamicResponse frenzyResponse = frenzy.AdvanceTo(.2f);
            return frenzyResponse.Stack == 2f && frenzyResponse.Desirable > ordinaryResponse.Desirable;
        }

        private static bool VerifyIrregularTimestamps()
        {
            AxiomDynamicsChannel first = NewChannel(AxiomKind.Mass);
            AxiomDynamicsChannel second = NewChannel(AxiomKind.Mass);
            float[] times = { 0f, .07f, .91f, 1.11f, 3.75f };
            float stack = 0f;
            for (int index = 0; index < times.Length; index++)
            {
                stack += 1f;
                first.ApplyInput(stack, 1f, times[index]);
                second.ApplyInput(stack, 1f, times[index]);
            }

            AxiomDynamicResponse left = first.AdvanceTo(4.2f);
            AxiomDynamicResponse right = second.AdvanceTo(4.2f);
            return Finite(left) && Finite(right)
                && Near(left.Desirable, right.Desirable) && Near(left.Counter, right.Counter);
        }

        private static bool VerifyLargeGap()
        {
            AxiomDynamicsChannel channel = NewChannel(AxiomKind.Heat);
            channel.ApplyInput(1f, 1f, 0f);
            AxiomDynamicResponse response = channel.AdvanceTo(100000f);
            return Finite(response) && response.Desirable >= 0f && response.Counter >= 0f;
        }

        private static bool VerifyFiniteBounds()
        {
            AxiomDynamicsChannel channel = NewChannel(AxiomKind.Mass);
            float stack = 0f;
            for (int index = 0; index < 256; index++)
            {
                stack += 2f;
                channel.ApplyInput(stack, 2f, index * .03f);
                AxiomDynamicResponse response = channel.AdvanceTo((index * .03f) + .02f);
                if (!Finite(response) || response.Desirable < 0f || response.Counter < 0f
                    || response.Desirable > 1.0001f || response.Counter > 1.0001f)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool VerifyProfiles()
        {
            AxiomDynamicsChannel heat = NewChannel(AxiomKind.Heat);
            AxiomDynamicsChannel order = NewChannel(AxiomKind.Order);
            heat.ApplyInput(2f, 2f, 0f);
            order.ApplyInput(2f, 2f, 0f);
            AxiomDynamicResponse heatResponse = heat.AdvanceTo(.3f);
            AxiomDynamicResponse orderResponse = order.AdvanceTo(.3f);
            return heatResponse.Kind == AxiomKind.Heat && orderResponse.Kind == AxiomKind.Order
                && !Near(heatResponse.Desirable, orderResponse.Desirable, .00001f);
        }

        private static AxiomDynamicResponse ApplyFour(AxiomDynamicsChannel channel, float interval)
        {
            float stack = 0f;
            for (int index = 0; index < 4; index++)
            {
                stack += 1f;
                channel.ApplyInput(stack, 1f, index * interval);
            }

            return channel.AdvanceTo(3f * interval);
        }

        private static AxiomDynamicsChannel NewChannel(AxiomKind kind)
        {
            return new AxiomDynamicsChannel(kind, AxiomDynamicsParameters.DefaultFor(kind));
        }

        private static bool Finite(AxiomDynamicResponse response)
        {
            return !float.IsNaN(response.Desirable) && !float.IsInfinity(response.Desirable)
                && !float.IsNaN(response.Counter) && !float.IsInfinity(response.Counter)
                && !float.IsNaN(response.Stack) && !float.IsInfinity(response.Stack);
        }

        private static bool Near(float left, float right, float tolerance = .0001f)
        {
            float difference = left - right;
            return difference <= tolerance && difference >= -tolerance;
        }
    }
}
