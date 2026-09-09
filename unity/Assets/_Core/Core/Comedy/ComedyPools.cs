// AUTO-GENERATED from tools/reference-engine/ludo/comedy.py by
// tools/generate_comedy_pools.py — DO NOT EDIT BY HAND.
// Regenerate with: python3 tools/generate_comedy_pools.py
// Data integrity is enforced by tools/reference-engine/tests/test_comedy.py.

namespace DesiLudo3D.Core
{
    /// <summary>Comedy reaction contexts (canonical order — mirrored
    /// from ludo/comedy.py CONTEXTS).</summary>
    public enum ComedyContext
    {
        Greeting = 0,
        Six = 1,
        Extra = 2,
        Release = 3,
        Capture = 4,
        Captured = 5,
        Void = 6,
        Nomove = 7,
        Finish = 8,
        Place = 9,
        Victory = 10,
        Defeat = 11,
        Leave = 12
    }

    /// <summary>One dialogue line: EN (romanized Hinglish) + HI
    /// (Devanagari) text and optional personality affinities
    /// (empty = any personality — A3/A4).</summary>
    public sealed class ComedyLine
    {
        public readonly string Id;
        public readonly string En;
        public readonly string Hi;
        public readonly PersonalityStyle[] Personalities;

        public ComedyLine(string id, string en, string hi,
                          PersonalityStyle[] personalities)
        {
            Id = id; En = en; Hi = hi; Personalities = personalities;
        }
    }

    /// <summary>The dialogue library (generated data). Selection
    /// logic lives in ComedyDirector; event mapping in ComedyEvents.</summary>
    public static class ComedyPools
    {
        public const int ContextCount = 13;
        public const int CategoryCount = 3;

        /// <summary>Context names in ComedyContext order (lowercase,
        /// mirrors the Python context strings).</summary>
        public static readonly string[] ContextNames =
        {
            "greeting", "six", "extra", "release", "capture", "captured", "void", "nomove", "finish", "place", "victory", "defeat", "leave",
        };

        /// <summary>R1 emotion tags per context (prerecorded clip
        /// lookup — never camera, Amendment A1).</summary>
        public static readonly string[] Emotions =
        {
            "happy", "excited", "excited", "excited", "laugh", "angry", "sad", "tired", "excited", "happy", "victory", "sad", "sad",
        };

        /// <summary>Pools[(int)context][(int)category] — never empty
        /// (asserted at generation time by the Python tests).</summary>
        public static readonly ComedyLine[][][] Pools = BuildPools();

        static ComedyLine[][][] BuildPools()
        {
            var p = new ComedyLine[13][][];
            p[0] = new ComedyLine[3][]
            {
                new ComedyLine[]
                {
                    new ComedyLine("greeting.male.0", "Namaste sabko! Aaj khel ka ustaad kaun? Hint: yahi khada hai.", "नमस्ते सबको! आज खेल का उस्ताद कौन? हिंट: यहीं खड़ा है।", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("greeting.male.1", "Ghar par baith ke bhi jeetunga — aur yahan toh maidan hi mera hai!", "घर पर बैठ के भी जीतूँगा — और यहाँ तो मैदान ही मेरा है!", new[] { PersonalityStyle.Overconfident }),
                    new ComedyLine("greeting.male.2", "Aaj ke episode mein: chaar bahadur, ek board, aur ek hi taqdeer!", "आज के एपिसोड में: चार बहादुर, एक बोर्ड, और एक ही तक़दीर!", new[] { PersonalityStyle.DramaKing }),
                    new ComedyLine("greeting.male.3", "Sab ready? Pasey mere dost hain — aaj bhi test kar lena.", "सब रेडी? पासे मेरे दोस्त हैं — आज भी टेस्ट कर लेना।", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("greeting.male.4", "Board garam hai, chai garam hai. Shuru karein?", "बोर्ड गरम है, चाय गरम है। शुरू करें?", System.Array.Empty<PersonalityStyle>()),
                },
                new ComedyLine[]
                {
                    new ComedyLine("greeting.female.0", "Hello ji! Chai ready hai, game shuru karein?", "हेलो जी! चाय रेडी है, गेम शुरू करें?", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("greeting.female.1", "Sab log smile ke saath khelenge, promise? Chalo, dhoom machate hain!", "सब लोग स्माइल के साथ खेलेंगे, प्रॉमिस? चलो, धूम मचाते हैं!", new[] { PersonalityStyle.SweetButDeadly }),
                    new ComedyLine("greeting.female.2", "Aaj ki episode mein: ek rani, chaar goliyan, aur full-on drama!", "आज के एपिसोड में: एक रानी, चार गोलियाँ, और फुल-ऑन ड्रामा!", new[] { PersonalityStyle.DramaKing }),
                    new ComedyLine("greeting.female.3", "Sabko namaste! Aaj ka game dil se khelenge, vaada.", "सबको नमस्ते! आज का गेम दिल से खेलेंगे, वादा।", new[] { PersonalityStyle.SweetButDeadly }),
                    new ComedyLine("greeting.female.4", "Aaj main hi trophy lekar jaungi — baaki sab practice samjho!", "आज मैं ही ट्रॉफी लेकर जाऊँगी — बाकी सब प्रैक्टिस समझो!", new[] { PersonalityStyle.Overconfident }),
                    new ComedyLine("greeting.female.5", "Hello sabko! Aaj ki rani kaun? Hint: yahi baithi hai.", "हेलो सबको! आज की रानी कौन? हिंट: यहीं बैठी है।", System.Array.Empty<PersonalityStyle>()),
                },
                new ComedyLine[]
                {
                    new ComedyLine("greeting.child.0", "Hello dosto! Aaj main sabko haraake ice cream lunga!", "हेलो दोस्तो! आज मैं सबको हराके आइस क्रीम लूँगा!", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("greeting.child.1", "Yahoo! Game start! Sab ready? One two three… GO!", "याहू! गेम स्टार्ट! सब रेडी? वन टू थ्री… गो!", new[] { PersonalityStyle.Excited }),
                    new ComedyLine("greeting.child.2", "Aaj ka champion main hi hoon — mumma ne bola hai!", "आज का चैंपियन मैं ही हूँ — मम्मा ने बोला है!", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("greeting.child.3", "Dhoondh… dhoondh… dhoondh macha denge aaj!", "धूँध… धूँध… धूँध मचा देंगे आज!", new[] { PersonalityStyle.DramaKing }),
                    new ComedyLine("greeting.child.4", "Sab aaye? Chalo shuru — pehle main!", "सब आए? चलो शुरू — पहले मैं!", System.Array.Empty<PersonalityStyle>()),
                },
            };
            p[1] = new ComedyLine[3][]
            {
                new ComedyLine[]
                {
                    new ComedyLine("six.male.0", "Chheee! Goli nikli ghar se — dhamaka hone wala hai!", "छहहह! गोली निकली घर से — धमाका होने वाला है!", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("six.male.1", "Chhah! Ye toh bas trailer tha, picture abhi baaki hai!", "छह! ये तो बस ट्रेलर था, पिक्चर अभी बाकी है!", new[] { PersonalityStyle.Overconfident }),
                    new ComedyLine("six.male.2", "Aaiiieee! Chhe! Meri kismat aaj puja kar rahi hai!", "आईईई! छह! मेरी किस्मत आज पूजा कर रही है!", new[] { PersonalityStyle.DramaKing }),
                    new ComedyLine("six.male.3", "Chhah! Ab toh rukna manaa hai.", "छह! अब तो रुकना मना है।", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("six.male.4", "Ek chhah, ek jaan!", "एक छह, एक जान!", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("six.male.5", "Chheee! Aaj toh board bhi nachega!", "छहहह! आज तो बोर्ड भी नाचेगा!", new[] { PersonalityStyle.DramaKing }),
                },
                new ComedyLine[]
                {
                    new ComedyLine("six.female.0", "Chhah aaya! Ab dekho, kaam kaise hota hai.", "छह आया! अब देखो, काम कैसे होता है।", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("six.female.1", "Aree vaah, chhah! Itna pyaara number — dil garden garden ho gaya.", "अरे वाह, छह! इतना प्यारा नंबर — दिल गार्डन गार्डन हो गया।", new[] { PersonalityStyle.SweetButDeadly }),
                    new ComedyLine("six.female.2", "Oho! Chhah! Camera on hota toh TRP aa jaata!", "ओहो! छह! कैमरा ऑन होता तो टीआरपी आ जाता!", new[] { PersonalityStyle.DramaKing }),
                    new ComedyLine("six.female.3", "Chhah! Dekha? Haath thoda halka hai aaj.", "छह! देखा? हाथ थोड़ा हल्का है आज।", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("six.female.4", "Chhah aaya. Ab bas jeet ka countdown hai.", "छह आया। अब बस जीत का काउंटडाउन है।", new[] { PersonalityStyle.Overconfident }),
                },
                new ComedyLine[]
                {
                    new ComedyLine("six.child.0", "Sixxx! Sabse best number aa gaya!", "सिक्स्स्स! सबसे बेस्ट नंबर आ गया!", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("six.child.1", "Chheeeeee! Dance time!", "छहहहहह! डांस टाइम!", new[] { PersonalityStyle.Excited }),
                    new ComedyLine("six.child.2", "Chhah! Superpower activate!", "छह! सुपरपावर एक्टिवेट!", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("six.child.3", "Ye wala number sabse fast hota hai!", "ये वाला नंबर सबसे फास्ट होता है!", new[] { PersonalityStyle.Excited }),
                },
            };
            p[2] = new ComedyLine[3][]
            {
                new ComedyLine[]
                {
                    new ComedyLine("extra.male.0", "Ek aur mauka! Aaj toh luck bhi mera fan hai.", "एक और मौका! आज तो लक भी मेरा फैन है।", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("extra.male.1", "Ek roll kam padta hai? Lo, ek aur free mein!", "एक रोल कम पड़ता है? लो, एक और फ्री में!", new[] { PersonalityStyle.Overconfident }),
                    new ComedyLine("extra.male.2", "Extra? Ye toh meri jaga pakki kar deta hai.", "एक्स्ट्रा? ये तो मेरी जगह पक्की कर देता है।", new[] { PersonalityStyle.Overconfident }),
                    new ComedyLine("extra.male.3", "Extra roll — jaise upar wale ne hi bheja ho.", "एक्स्ट्रा रोल — जैसे ऊपर वाले ने ही भेजा हो।", System.Array.Empty<PersonalityStyle>()),
                },
                new ComedyLine[]
                {
                    new ComedyLine("extra.female.0", "Ek aur roll? Lagta hai aaj ka din mera hai ji.", "एक और रोल? लगता है आज का दिन मेरा है जी।", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("extra.female.1", "Thank you, lucky stars! Ek aur mauka mil gaya.", "थैंक यू, लकी स्टार्स! एक और मौका मिल गया।", new[] { PersonalityStyle.SweetButDeadly }),
                    new ComedyLine("extra.female.2", "Ek aur mauka! Aaj toh luck mera saath de raha hai.", "एक और मौका! आज तो लक मेरा साथ दे रहा है।", System.Array.Empty<PersonalityStyle>()),
                },
                new ComedyLine[]
                {
                    new ComedyLine("extra.child.0", "Again again! Ek aur baar!", "अगेन अगेन! एक और बार!", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("extra.child.1", "Ek aur chance! Ab dekhna kya hota hai!", "एक और चांस! अब देखना क्या होता है!", new[] { PersonalityStyle.Excited }),
                    new ComedyLine("extra.child.2", "Bonus turn! Double maza!", "बोनस टर्न! डबल मज़ा!", System.Array.Empty<PersonalityStyle>()),
                },
            };
            p[3] = new ComedyLine[3][]
            {
                new ComedyLine[]
                {
                    new ComedyLine("release.male.0", "Aadmi bahar aaya. Ab game shuru.", "आदमी बाहर आया। अब गेम शुरू।", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("release.male.1", "Bahar aao hero! Maidan tumhara intezaar kar raha hai.", "बाहर आओ हीरो! मैदान तुम्हारा इंतज़ार कर रहा है।", new[] { PersonalityStyle.Overconfident }),
                    new ComedyLine("release.male.2", "Bahar aaya khiladi. Ab game mein dam aaya.", "बाहर आया खिलाड़ी। अब गेम में दम आया।", System.Array.Empty<PersonalityStyle>()),
                },
                new ComedyLine[]
                {
                    new ComedyLine("release.female.0", "Meri goli bahar! Ab sambhal ke rehna, sab log.", "मेरी गोली बाहर! अब संभल के रहना, सब लोग।", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("release.female.1", "Lift-off! Rocket chhoot gaya base se!", "लिफ्ट-ऑफ! रॉकेट छूट गया बेस से!", new[] { PersonalityStyle.DramaKing }),
                    new ComedyLine("release.female.2", "Entry ho gayi! Ab sab ke kaan khade rakhna.", "एंट्री हो गई! अब सब के कान खड़े रखना।", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("release.female.3", "Lights, camera, goli!", "लाइट्स, कैमरा, गोली!", new[] { PersonalityStyle.DramaKing }),
                },
                new ComedyLine[]
                {
                    new ComedyLine("release.child.0", "Mera soldier nikla! Salute!", "मेरा सोल्जर निकला! सैल्यूट!", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("release.child.1", "Bahar aaya! Ready ho jao dushmano!", "बाहर आया! रेडी हो जाओ दुश्मनों!", new[] { PersonalityStyle.Excited }),
                    new ComedyLine("release.child.2", "Naya soldier ready hai! Attention!", "नया सोल्जर रेडी है! अटेंशन!", System.Array.Empty<PersonalityStyle>()),
                },
            };
            p[4] = new ComedyLine[3][]
            {
                new ComedyLine[]
                {
                    new ComedyLine("capture.male.0", "Cut! Seedha ghar bhej diya. Koi baat nahi — phir aana.", "काट! सीधा घर भेज दिया। कोई बात नहीं — फिर आना।", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("capture.male.1", "Dekha? Mera raasta, meri marzi!", "देखा? मेरा रास्ता, मेरी मर्ज़ी!", new[] { PersonalityStyle.Overconfident }),
                    new ComedyLine("capture.male.2", "Aur cut! Ek aur safar! Audience, taaliyan bajao!", "और काट! एक और सफ़र! ऑडियंस, तालियाँ बजाओ!", new[] { PersonalityStyle.DramaKing }),
                    new ComedyLine("capture.male.3", "Bilkul professional cut. Bina bhed-bhav.", "बिल्कुल प्रोफेशनल काट। बिना भेदभाव।", new[] { PersonalityStyle.Balanced }),
                    new ComedyLine("capture.male.4", "Kaato, bhaago, jeeto — simple funda hai mera.", "काटो, भागो, जीतो — सिंपल फंडा है मेरा।", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("capture.male.5", "Aur ek shikaar! Sher aaj bhookha hai!", "और एक शिकार! शेर आज भूखा है!", new[] { PersonalityStyle.DramaKing }),
                },
                new ComedyLine[]
                {
                    new ComedyLine("capture.female.0", "Sorry ji, aapka token ab ghar ke raaste mein hai.", "सॉरी जी, आपका टोकन अब घर के रास्ते में है।", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("capture.female.1", "Pyaar se kaat diya — ouch, lagta hai thoda zyada hi pyaar ho gaya.", "प्यार से काट दिया — आऊच, लगता है थोड़ा ज़्यादा ही प्यार हो गया।", new[] { PersonalityStyle.SweetButDeadly }),
                    new ComedyLine("capture.female.2", "Oho! Seedha ghar! Ye kaisa twist tha — mera hi haath, mera hi zulm!", "ओहो! सीधा घर! ये कैसा ट्विस्ट था — मेरा ही हाथ, मेरा ही ज़ुल्म!", new[] { PersonalityStyle.DramaKing }),
                    new ComedyLine("capture.female.3", "Business as usual. Koi personal baat nahi thi.", "बिज़नेस ऐज़ यूज़ुअल। कोई पर्सनल बात नहीं थी।", new[] { PersonalityStyle.Balanced }),
                    new ComedyLine("capture.female.4", "Cut gaya. Business decision tha, personal nahi.", "कट गया। बिज़नेस डिसीज़न था, पर्सनल नहीं।", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("capture.female.5", "Aapke token ka ticket cut ho gaya — mera raasta clear hai.", "आपके टोकन का टिकट कट गया — मेरा रास्ता क्लियर है।", new[] { PersonalityStyle.Overconfident }),
                },
                new ComedyLine[]
                {
                    new ComedyLine("capture.child.0", "Cut cut cut! Tum ghar jao, main aage badhunga!", "काट काट काट! तुम घर जाओ, मैं आगे बढ़ूँगा!", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("capture.child.1", "Yayyy! Tumhari goli ghar gayi! Bye bye!", "येईईई! तुम्हारी गोली घर गई! बाय बाय!", new[] { PersonalityStyle.Excited }),
                    new ComedyLine("capture.child.2", "Isi vyavastha ke khilaaf kranti hogi… tumhari goli gayi!", "इसी व्यवस्था के खिलाफ़ क्रांति होगी… तुम्हारी गोली गई!", new[] { PersonalityStyle.DramaKing }),
                    new ComedyLine("capture.child.3", "Kaat diya! Sorry… thoda sa hi!", "काट दिया! सॉरी… थोड़ा सा ही!", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("capture.child.4", "Zoom! Seedha ghar bhej diya!", "ज़ूम! सीधा घर भेज दिया!", new[] { PersonalityStyle.Excited }),
                },
            };
            p[5] = new ComedyLine[3][]
            {
                new ComedyLine[]
                {
                    new ComedyLine("captured.male.0", "Arre! Itni mehnat bekaar? Theek hai — badla lenge.", "अरे! इतनी मेहनत बेकार? ठीक है — बदला लेंगे।", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("captured.male.1", "Galti se hua. Wapas aake dikhaunga asli game.", "गलती से हुआ। वापस आके दिखाऊँगा असली गेम।", new[] { PersonalityStyle.Overconfident }),
                    new ComedyLine("captured.male.2", "Naaaheeee! Meri goli! Mera sapna! Sab barbaad!", "नहीहीहीं! मेरी गोली! मेरा सपना! सब बर्बाद!", new[] { PersonalityStyle.DramaKing }),
                    new ComedyLine("captured.male.3", "Ghar bhej diya… chalo rest karta hai.", "घर भेज दिया… चलो रेस्ट करता है।", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("captured.male.4", "Theek hai, ye wala le lo. Baaki teen abhi bhi mere paas hain.", "ठीक है, ये वाला ले लो। बाकी तीन अभी भी मेरे पास हैं।", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("captured.male.5", "Barbaad! Sab barbaad! Ek minute — teen token bache hain.", "बर्बाद! सब बर्बाद! एक मिनट — तीन टोकन बचे हैं।", new[] { PersonalityStyle.DramaKing }),
                },
                new ComedyLine[]
                {
                    new ComedyLine("captured.female.0", "Hai rabba! Chalo, dobara try karte hain.", "हाय रब्बा! चलो, दोबारा ट्राई करते हैं।", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("captured.female.1", "Ye kya ho gaya! Poore game ki izzat daav pe lag gayi!", "ये क्या हो गया! पूरे गेम की इज़्ज़त दाँव पे लग गई!", new[] { PersonalityStyle.DramaKing }),
                    new ComedyLine("captured.female.2", "Koi baat nahi ji. Ghar jaake rest karo, hum phir aayenge.", "कोई बात नहीं जी। घर जाके रेस्ट करो, हम फिर आएँगे।", new[] { PersonalityStyle.SweetButDeadly }),
                    new ComedyLine("captured.female.3", "Oye! Meri goli! …Chalo, agli baar dhyan rakhungi.", "ओए! मेरी गोली! …चलो, अगली बार ध्यान रखूँगी।", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("captured.female.4", "Galti tumhari goli ne ki hai, meri nahi.", "गलती तुम्हारी गोली ने की है, मेरी नहीं।", new[] { PersonalityStyle.Overconfident }),
                },
                new ComedyLine[]
                {
                    new ComedyLine("captured.child.0", "Oye! Not fair! Wapas aaunga main!", "ओए! नॉट फेयर! वापस आऊँगा मैं!", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("captured.child.1", "Mummyyyy! Ye meri goli kaat diya!", "मम्मीईई! ये मेरी गोली काट दिया!", new[] { PersonalityStyle.DramaKing }),
                    new ComedyLine("captured.child.2", "Gaya kaata! Chalo, phir se bhejenge — double speed!", "गया काटा! चलो, फिर से भेजेंगे — डबल स्पीड!", new[] { PersonalityStyle.Excited }),
                    new ComedyLine("captured.child.3", "Cheating! Main abhi wapas aaunga!", "चीटिंग! मैं अभी वापस आऊँगा!", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("captured.child.4", "Wow, itna upar se aaya tha?! …ok, ghar chala gaya.", "वाह, इतना ऊपर से आया था?! …ओके, घर चला गया।", new[] { PersonalityStyle.Excited }),
                },
            };
            p[6] = new ComedyLine[3][]
            {
                new ComedyLine[]
                {
                    new ComedyLine("void.male.0", "Teen chhah… aur sab vanish? Kismat ka sense of humour mast hai.", "तीन छह… और सब गायब? किस्मत का सेंस ऑफ ह्यूमर मस्त है।", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("void.male.1", "Teen chhah! TEEN CHHAH! Aur kaam zero! Ye kaisa twist hai bhai?", "तीन छह! तीन छह! और काम ज़ीरो! ये कैसा ट्विस्ट है भाई?", new[] { PersonalityStyle.DramaKing }),
                    new ComedyLine("void.male.2", "Rule book ne galti ki hai. Main toh abhi bhi aage hoon.", "रूल बुक ने गलती की है। मैं तो अभी भी आगे हूँ।", new[] { PersonalityStyle.Overconfident }),
                    new ComedyLine("void.male.3", "Teen chhah… zero kaam. Statistics ka kamaal.", "तीन छह… ज़ीरो काम। स्टैटिस्टिक्स का कमाल।", System.Array.Empty<PersonalityStyle>()),
                },
                new ComedyLine[]
                {
                    new ComedyLine("void.female.0", "Teen chhah aur kaam zero — director sahab, ye screenplay theek nahi hai.", "तीन छह और काम ज़ीरो — डायरेक्टर साहब, ये स्क्रीनप्ले ठीक नहीं है।", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("void.female.1", "Oh no no no! Teen chhah gayab?! Main is scene se khush nahi hoon!", "ओह नो नो नो! तीन छह गायब?! मैं इस सीन से खुश नहीं हूँ!", new[] { PersonalityStyle.DramaKing }),
                    new ComedyLine("void.female.2", "Chalo, achhi baat hai — doosron ko bhi mauka milega ji.", "चलो, अच्छी बात है — दूसरों को भी मौका मिलेगा जी।", new[] { PersonalityStyle.SweetButDeadly }),
                    new ComedyLine("void.female.3", "Teen chhah aur sab band? Ye kaisa suspense twist hai!", "तीन छह और सब बंद? ये कैसा सस्पेंस ट्विस्ट है!", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("void.female.4", "Meri teen sixes ki party cancel ho gayi! Kisne cancel ki?!", "मेरी थ्री सिक्सेस की पार्टी कैंसल हो गई! किसने कैंसल की?!", new[] { PersonalityStyle.DramaKing }),
                },
                new ComedyLine[]
                {
                    new ComedyLine("void.child.0", "Sab… gayab?! Ek minute pehle sab badhiya tha!", "सब… गायब?! एक मिनट पहले सब बढ़िया था!", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("void.child.1", "Ye game khelne layak nahi hai! Main complain karunga!", "ये गेम खेलने लायक नहीं है! मैं कंप्लेन करूँगा!", new[] { PersonalityStyle.DramaKing }),
                    new ComedyLine("void.child.2", "Sab ulta ho gaya! Bhagwan ji, aap bhi na!", "सब उल्टा हो गया! भगवान जी, आप भी ना!", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("void.child.3", "Sab zero?! Full drama episode!", "सब ज़ीरो?! फुल ड्रामा एपिसोड!", new[] { PersonalityStyle.Excited }),
                },
            };
            p[7] = new ComedyLine[3][]
            {
                new ComedyLine[]
                {
                    new ComedyLine("nomove.male.0", "Move hi nahi? Chalo, chai break.", "मूव ही नहीं? चलो, चाय ब्रेक।", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("nomove.male.1", "Main strategy banata hoon, aur patta khali? Unbelievable.", "मैं स्ट्रैटजी बनाता हूँ, और पत्ता खाली? अनबिलीवेबल।", new[] { PersonalityStyle.Overconfident }),
                    new ComedyLine("nomove.male.2", "Bina chale bhi style hai mera.", "बिना चले भी स्टाइल है मेरा।", System.Array.Empty<PersonalityStyle>()),
                },
                new ComedyLine[]
                {
                    new ComedyLine("nomove.female.0", "Koi move nahi? Theek hai. Patience sab kuch hai.", "कोई मूव नहीं? ठीक है। पेशेंस सब कुछ है।", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("nomove.female.1", "Ye turn officially cancelled samajhiye. Aage badhiye.", "ये टर्न ऑफिशियली कैंसल्ड समझिए। आगे बढ़िए।", new[] { PersonalityStyle.DramaKing }),
                    new ComedyLine("nomove.female.2", "Intezaar ka phal meetha hota hai. Next turn dekhte hain.", "इंतज़ार का फल मीठा होता है। नेक्स्ट टर्न देखते हैं।", System.Array.Empty<PersonalityStyle>()),
                },
                new ComedyLine[]
                {
                    new ComedyLine("nomove.child.0", "Kuch nahi chala? Okie — next time double speed!", "कुछ नहीं चला? ओके — नेक्स्ट टाइम डबल स्पीड!", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("nomove.child.1", "Koi baat nahi! Main toh bas thoda rest kar raha tha!", "कोई बात नहीं! मैं तो बस थोड़ा रेस्ट कर रहा था!", new[] { PersonalityStyle.Excited }),
                    new ComedyLine("nomove.child.2", "Break time! Paani pi lo sab.", "ब्रेक टाइम! पानी पी लो सब।", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("nomove.child.3", "Bas! Main is move se khush nahi!", "बस! मैं इस मूव से खुश नहीं!", new[] { PersonalityStyle.DramaKing }),
                },
            };
            p[8] = new ComedyLine[3][]
            {
                new ComedyLine[]
                {
                    new ComedyLine("finish.male.0", "Ek ghar pahuncha. Teen bache. Hisaab barabar hai.", "एक घर पहुँचा। तीन बचे। हिसाब बराबर है।", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("finish.male.1", "Dekho aur seekho — aisa hota hai finish.", "देखो और सीखो — ऐसा होता है फिनिश।", new[] { PersonalityStyle.Overconfident }),
                    new ComedyLine("finish.male.2", "Ek aur andar. Board pe dhamaka continue.", "एक और अंदर। बोर्ड पे धमाका कंटीन्यू।", System.Array.Empty<PersonalityStyle>()),
                },
                new ComedyLine[]
                {
                    new ComedyLine("finish.female.0", "Ek pahunch gayi! Ab bas thoda aur dhyan se.", "एक पहुँच गई! अब बस थोड़ा और ध्यान से।", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("finish.female.1", "Wah! Ghar ki mandir mein aa gayi meri goli. Pranam.", "वाह! घर के मंदिर में आ गई मेरी गोली। प्रणाम।", new[] { PersonalityStyle.SweetButDeadly }),
                    new ComedyLine("finish.female.2", "Swagat! Ghar ki thali mein aake baithi goli.", "स्वागत! घर की थाली में आके बैठी गोली।", System.Array.Empty<PersonalityStyle>()),
                },
                new ComedyLine[]
                {
                    new ComedyLine("finish.child.0", "Ghar aa gaya! Mummy, dekho — main jeet raha hoon!", "घर आ गया! मम्मी, देखो — मैं जीत रहा हूँ!", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("finish.child.1", "Home run! Ek down, teen to go!", "होम रन! एक डाउन, थ्री टू गो!", new[] { PersonalityStyle.Excited }),
                    new ComedyLine("finish.child.2", "Mera ek kaam poora! Teen bache hain!", "मेरा एक काम पूरा! तीन बचे हैं!", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("finish.child.3", "Ghar pahunchi! Ek aur bhejni hai!", "घर पहुँची! एक और भेजनी है!", new[] { PersonalityStyle.Excited }),
                },
            };
            p[9] = new ComedyLine[3][]
            {
                new ComedyLine[]
                {
                    new ComedyLine("place.male.0", "Doosra number? Acceptable. Top class ke bahar kuch nahi hota.", "दूसरा नंबर? एक्सेप्टेबल। टॉप क्लास के बाहर कुछ नहीं होता।", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("place.male.1", "Itne kareeb… ITNE KAREEB… aur phir bhi doosra! Kismat, tu bhi na!", "इतने क़रीब… इतने क़रीब… और फिर भी दूसरा! किस्मत, तू भी ना!", new[] { PersonalityStyle.DramaKing }),
                },
                new ComedyLine[]
                {
                    new ComedyLine("place.female.0", "Achhi jagah hai. Upar wale ko manzoor tha itna hi aaj.", "अच्छी जगह है। ऊपर वाले को मंज़ूर था इतना ही आज।", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("place.female.1", "Runner-up bhi bahut achha hai ji. Mithai sabko milegi!", "रनर-अप भी बहुत अच्छा है जी। मिठाई सबको मिलेगी!", new[] { PersonalityStyle.SweetButDeadly }),
                    new ComedyLine("place.female.2", "Doosri jagah? Not bad! Doosre number ki bhi apni alag khushboo hai.", "दूसरी जगह? नॉट बैड! दूसरे नंबर की भी अपनी अलग खुशबू है।", System.Array.Empty<PersonalityStyle>()),
                },
                new ComedyLine[]
                {
                    new ComedyLine("place.child.0", "Number do? Almost winner bolte hain usko!", "नंबर दो? ऑलमोस्ट विनर बोलते हैं उसको!", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("place.child.1", "Doosra bhi jeet jaisa hai! Star main hoon!", "दूसरा भी जीत जैसा है! स्टार मैं हूँ!", new[] { PersonalityStyle.Excited }),
                    new ComedyLine("place.child.2", "Runner-up? Pro level runner-up, please.", "रनर-अप? प्रो लेवल रनर-अप, प्लीज़।", System.Array.Empty<PersonalityStyle>()),
                },
            };
            p[10] = new ComedyLine[3][]
            {
                new ComedyLine[]
                {
                    new ComedyLine("victory.male.0", "Aaj ka champion main hoon. Kal kaun? Dekha jayega.", "आज का चैंपियन मैं हूँ। कल कौन? देखा जाएगा।", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("victory.male.1", "Bolo? Kaun bola tha pehle? MAIN bola tha pehle!", "बोलो? कौन बोला था पहले? मैं बोला था पहले!", new[] { PersonalityStyle.Overconfident }),
                    new ComedyLine("victory.male.2", "VICTORY! History likhi gayi — is board par, in haathon se!", "विक्ट्री! हिस्ट्री लिखी गई — इस बोर्ड पर, इन हाथों से!", new[] { PersonalityStyle.DramaKing }),
                    new ComedyLine("victory.male.3", "Jeet gaya! Ab sab mujhe 'champion' bulaenge.", "जीत गया! अब सब मुझे 'चैंपियन' बुलाएँगे।", System.Array.Empty<PersonalityStyle>()),
                },
                new ComedyLine[]
                {
                    new ComedyLine("victory.female.0", "Jeet gayi! Ab sab ke liye mithai meri taraf se!", "जीत गई! अब सब के लिए मिठाई मेरी तरफ़ से!", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("victory.female.1", "Aaj ki episode ka climax: rani jeet gayi! The End — happy ending!", "आज के एपिसोड का क्लाइमैक्स: रानी जीत गई! द एंड — हैप्पी एंडिंग!", new[] { PersonalityStyle.DramaKing }),
                    new ComedyLine("victory.female.2", "Sab ne achha khela. Par aaj mera din tha, sach much!", "सब ने अच्छा खेला। पर आज मेरा दिन था, सच मुच्छ!", new[] { PersonalityStyle.SweetButDeadly }),
                    new ComedyLine("victory.female.3", "Jeet meri hai! Photo kheechne ka time hai.", "जीत मेरी है! फ़ोटो खींचने का टाइम है।", System.Array.Empty<PersonalityStyle>()),
                },
                new ComedyLine[]
                {
                    new ComedyLine("victory.child.0", "MAIN JEETA! Ice cream time!", "मैं जीता! आइस क्रीम टाइम!", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("victory.child.1", "Winner winner! Aaj dinner meri taraf se… mummy se puch ke!", "विनर विनर! आज डिनर मेरी तरफ़ से… मम्मी से पूछ के!", new[] { PersonalityStyle.Excited }),
                    new ComedyLine("victory.child.2", "Main jeeta main jeeta! Mummy, dekha?", "मैं जीता मैं जीता! मम्मी, देखा?", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("victory.child.3", "Trophy mera naam bolegi! Bing bing!", "ट्रॉफी मेरा नाम बोलेगी! बिंग बिंग!", new[] { PersonalityStyle.Excited }),
                },
            };
            p[11] = new ComedyLine[3][]
            {
                new ComedyLine[]
                {
                    new ComedyLine("defeat.male.0", "Aaj aapki, kal hamari. Revanche fixed hai.", "आज आपकी, कल हमारी। रिवांश फिक्स्ड है।", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("defeat.male.1", "Main haar nahi maanta. Main bas… aaj ke liye ke maan gaya.", "मैं हार नहीं मानता। मैं बस… आज के लिए के मान गया।", new[] { PersonalityStyle.DramaKing }),
                    new ComedyLine("defeat.male.2", "Achha khela aapne. Respect — aur revanche.", "अच्छा खेला आपने। रेस्पेक्ट — और रिवांश।", System.Array.Empty<PersonalityStyle>()),
                },
                new ComedyLine[]
                {
                    new ComedyLine("defeat.female.0", "Achha khela aapne. Next game ki taiyari shuru!", "अच्छा खेला आपने। नेक्स्ट गेम की तैयारी शुरू!", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("defeat.female.1", "Aap jeete, dil se badhai. Par agli baar dhyan rakhna ji!", "आप जीते, दिल से बधाई। पर अगली बार ध्यान रखना जी!", new[] { PersonalityStyle.SweetButDeadly }),
                    new ComedyLine("defeat.female.2", "Aaj aapki shan hai. Kal meri hogi.", "आज आपकी शान है। कल मेरी होगी।", System.Array.Empty<PersonalityStyle>()),
                },
                new ComedyLine[]
                {
                    new ComedyLine("defeat.child.0", "Thoda sa hi haar gaya main. Next time full jeetunga!", "थोड़ा सा ही हार गया मैं। नेक्स्ट टाइम फुल जीतूँगा!", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("defeat.child.1", "Bas! Aaj mood kharab hai. Kal dekhte hain sab.", "बस! आज मूड खराब है। कल देखते हैं सब।", new[] { PersonalityStyle.DramaKing }),
                    new ComedyLine("defeat.child.2", "Koi baat nahi. Main abhi strong ban raha hoon.", "कोई बात नहीं। मैं अभी स्ट्रॉन्ग बन रहा हूँ।", System.Array.Empty<PersonalityStyle>()),
                },
            };
            p[12] = new ComedyLine[3][]
            {
                new ComedyLine[]
                {
                    new ComedyLine("leave.male.0", "Ja rahe ho? Theek hai — game yaad rakhenge.", "जा रहे हो? ठीक है — गेम याद रखेंगे।", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("leave.male.1", "Aur ek yoddha chhod ke gaya… itihaas unhe yaad rakhega.", "और एक योद्धा छोड़ के गया… इतिहास उन्हें याद रखेगा।", new[] { PersonalityStyle.DramaKing }),
                },
                new ComedyLine[]
                {
                    new ComedyLine("leave.female.0", "Jaldi se aana ji, hum log yahin hain.", "जल्दी से आना जी, हम लोग यहीं हैं।", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("leave.female.1", "Sambhal ke jaiye ji. Yahan sab theek rahega.", "संभल के जाइए जी। यहाँ सब ठीक रहेगा।", new[] { PersonalityStyle.SweetButDeadly }),
                },
                new ComedyLine[]
                {
                    new ComedyLine("leave.child.0", "Bye bye! Jaldi aana — promise?", "बाय बाय! जल्दी आना — प्रॉमिस?", System.Array.Empty<PersonalityStyle>()),
                    new ComedyLine("leave.child.1", "Arre ruko! Last roll toh karke jao!", "अरे रुको! लास्ट रोल तो करके जाओ!", new[] { PersonalityStyle.Excited }),
                },
            };
            return p;
        }
    }
}
