# -*- coding: utf-8 -*-
"""Comedy engine reference implementation (M3).

Original Hinglish/English dialogue library + the ReactionDirector selection
algorithm. This module is the curated SOURCE OF TRUTH for the dialogue data:
`tools/generate_comedy_pools.py` emits the C# `ComedyPools.cs` from it
(hash-verified, same protocol as the rules sync, IL-014/IL-020).

Amendment compliance:
- A3: female and child pools are natively written per category — NEVER male
  lines with word swaps. Personality variants bias selection.
- A4: lines are written to be SPOKEN first (bubbles are the secondary,
  accessibility surface — each line carries en + hi).
- No copyrighted movie dialogue, no celebrity/real-person imitation.

The selector: seeded deterministic RNG (splitmix64, mirrored exactly in C#),
per-seat no-repeat history, per-(seat, context) cooldown, personality bias.
"""

from typing import Dict, List, Optional, Tuple

from .model import (
    DiceRolled, GameEvent, MatchEnded, NoLegalMove, PlayerFinished,
    TokenCaptured, TokenFinished, TokenMoved, TurnVoided,
)

MASK = 0xFFFFFFFFFFFFFFFF

CATEGORIES = ("male", "female", "child")
PERSONALITIES = ("balanced", "overconfident", "drama", "sweet", "excited")

# Contexts, in canonical order (mirrored by the C# ComedyContext enum).
CONTEXTS = (
    "greeting",    # match start
    "six",         # rolled a 6
    "extra",       # extra roll used
    "release",     # token released from base
    "capture",     # this seat captured someone
    "captured",    # this seat's token was captured
    "void",        # three sixes, turn voided
    "nomove",      # no legal move
    "finish",      # one token reached home
    "place",       # finished the match, not 1st
    "victory",     # won (or finished 1st)
    "defeat",      # match ended, didn't win
    "leave",       # another player left (reserved for M4 flows)
)

# Emotion tags used by R1 prerecorded clip lookup (never camera — A1).
EMOTIONS = {
    "greeting": "happy", "six": "excited", "extra": "excited",
    "release": "excited", "capture": "laugh", "captured": "angry",
    "void": "sad", "nomove": "tired", "finish": "excited",
    "place": "happy", "victory": "victory", "defeat": "sad",
    "leave": "sad",
}

NO_COOLDOWN_CONTEXTS = ("greeting", "victory", "defeat")


class Line:
    __slots__ = ("id", "context", "category", "personalities", "en", "hi")

    def __init__(self, id, context, category, personalities, en, hi):
        self.id = id
        self.context = context
        self.category = category
        self.personalities = tuple(personalities)
        self.en = en
        self.hi = hi


_ID_COUNTER: Dict[Tuple[str, str], int] = {}


def _l(context, category, personalities, en, hi):
    idx = _ID_COUNTER.get((context, category), 0)
    _ID_COUNTER[(context, category)] = idx + 1
    return Line("%s.%s.%d" % (context, category, idx), context,
                category, personalities, en, hi)


LINES: List[Line] = [
    # ------------------------------------------------------------ greeting
    _l("greeting", "male", [], "Namaste sabko! Aaj khel ka ustaad kaun? Hint: yahi khada hai.",
       "नमस्ते सबको! आज खेल का उस्ताद कौन? हिंट: यहीं खड़ा है।"),
    _l("greeting", "male", ["overconfident"], "Ghar par baith ke bhi jeetunga — aur yahan toh maidan hi mera hai!",
       "घर पर बैठ के भी जीतूँगा — और यहाँ तो मैदान ही मेरा है!"),
    _l("greeting", "male", ["drama"], "Aaj ke episode mein: chaar bahadur, ek board, aur ek hi taqdeer!",
       "आज के एपिसोड में: चार बहादुर, एक बोर्ड, और एक ही तक़दीर!"),
    _l("greeting", "female", [], "Hello ji! Chai ready hai, game shuru karein?",
       "हेलो जी! चाय रेडी है, गेम शुरू करें?"),
    _l("greeting", "female", ["sweet"], "Sab log smile ke saath khelenge, promise? Chalo, dhoom machate hain!",
       "सब लोग स्माइल के साथ खेलेंगे, प्रॉमिस? चलो, धूम मचाते हैं!"),
    _l("greeting", "female", ["drama"], "Aaj ki episode mein: ek rani, chaar goliyan, aur full-on drama!",
       "आज के एपिसोड में: एक रानी, चार गोलियाँ, और फुल-ऑन ड्रामा!"),
    _l("greeting", "child", [], "Hello dosto! Aaj main sabko haraake ice cream lunga!",
       "हेलो दोस्तो! आज मैं सबको हराके आइस क्रीम लूँगा!"),
    _l("greeting", "child", ["excited"], "Yahoo! Game start! Sab ready? One two three… GO!",
       "याहू! गेम स्टार्ट! सब रेडी? वन टू थ्री… गो!"),

    # ------------------------------------------------------------ six
    _l("six", "male", [], "Chheee! Goli nikli ghar se — dhamaka hone wala hai!",
       "छहहह! गोली निकली घर से — धमाका होने वाला है!"),
    _l("six", "male", ["overconfident"], "Chhah! Ye toh bas trailer tha, picture abhi baaki hai!",
       "छह! ये तो बस ट्रेलर था, पिक्चर अभी बाकी है!"),
    _l("six", "male", ["drama"], "Aaiiieee! Chhe! Meri kismat aaj puja kar rahi hai!",
       "आईईई! छह! मेरी किस्मत आज पूजा कर रही है!"),
    _l("six", "female", [], "Chhah aaya! Ab dekho, kaam kaise hota hai.",
       "छह आया! अब देखो, काम कैसे होता है।"),
    _l("six", "female", ["sweet"], "Aree vaah, chhah! Itna pyaara number — dil garden garden ho gaya.",
       "अरे वाह, छह! इतना प्यारा नंबर — दिल गार्डन गार्डन हो गया।"),
    _l("six", "female", ["drama"], "Oho! Chhah! Camera on hota toh TRP aa jaata!",
       "ओहो! छह! कैमरा ऑन होता तो टीआरपी आ जाता!"),
    _l("six", "child", [], "Sixxx! Sabse best number aa gaya!",
       "सिक्स्स्स! सबसे बेस्ट नंबर आ गया!"),
    _l("six", "child", ["excited"], "Chheeeeee! Dance time!",
       "छहहहहह! डांस टाइम!"),

    # ------------------------------------------------------------ extra
    _l("extra", "male", [], "Ek aur mauka! Aaj toh luck bhi mera fan hai.",
       "एक और मौका! आज तो लक भी मेरा फैन है।"),
    _l("extra", "male", ["overconfident"], "Ek roll kam padta hai? Lo, ek aur free mein!",
       "एक रोल कम पड़ता है? लो, एक और फ्री में!"),
    _l("extra", "female", [], "Ek aur roll? Lagta hai aaj ka din mera hai ji.",
       "एक और रोल? लगता है आज का दिन मेरा है जी।"),
    _l("extra", "female", ["sweet"], "Thank you, lucky stars! Ek aur mauka mil gaya.",
       "थैंक यू, लकी स्टार्स! एक और मौका मिल गया।"),
    _l("extra", "child", [], "Again again! Ek aur baar!",
       "अगेन अगेन! एक और बार!"),
    _l("extra", "child", ["excited"], "Ek aur chance! Ab dekhna kya hota hai!",
       "एक और चांस! अब देखना क्या होता है!"),

    # ------------------------------------------------------------ release
    _l("release", "male", [], "Aadmi bahar aaya. Ab game shuru.",
       "आदमी बाहर आया। अब गेम शुरू।"),
    _l("release", "male", ["overconfident"], "Bahar aao hero! Maidan tumhara intezaar kar raha hai.",
       "बाहर आओ हीरो! मैदान तुम्हारा इंतज़ार कर रहा है।"),
    _l("release", "female", [], "Meri goli bahar! Ab sambhal ke rehna, sab log.",
       "मेरी गोली बाहर! अब संभल के रहना, सब लोग।"),
    _l("release", "female", ["drama"], "Lift-off! Rocket chhoot gaya base se!",
       "लिफ्ट-ऑफ! रॉकेट छूट गया बेस से!"),
    _l("release", "child", [], "Mera soldier nikla! Salute!",
       "मेरा सोल्जर निकला! सैल्यूट!"),
    _l("release", "child", ["excited"], "Bahar aaya! Ready ho jao dushmano!",
       "बाहर आया! रेडी हो जाओ दुश्मनों!"),

    # ------------------------------------------------------------ capture (attacker)
    _l("capture", "male", [], "Cut! Seedha ghar bhej diya. Koi baat nahi — phir aana.",
       "काट! सीधा घर भेज दिया। कोई बात नहीं — फिर आना।"),
    _l("capture", "male", ["overconfident"], "Dekha? Mera raasta, meri marzi!",
       "देखा? मेरा रास्ता, मेरी मर्ज़ी!"),
    _l("capture", "male", ["drama"], "Aur cut! Ek aur safar! Audience, taaliyan bajao!",
       "और काट! एक और सफ़र! ऑडियंस, तालियाँ बजाओ!"),
    _l("capture", "male", ["balanced"], "Bilkul professional cut. Bina bhed-bhav.",
       "बिल्कुल प्रोफेशनल काट। बिना भेदभाव।"),
    _l("capture", "female", [], "Sorry ji, aapka token ab ghar ke raaste mein hai.",
       "सॉरी जी, आपका टोकन अब घर के रास्ते में है।"),
    _l("capture", "female", ["sweet"], "Pyaar se kaat diya — ouch, lagta hai thoda zyada hi pyaar ho gaya.",
       "प्यार से काट दिया — आऊच, लगता है थोड़ा ज़्यादा ही प्यार हो गया।"),
    _l("capture", "female", ["drama"], "Oho! Seedha ghar! Ye kaisa twist tha — mera hi haath, mera hi zulm!",
       "ओहो! सीधा घर! ये कैसा ट्विस्ट था — मेरा ही हाथ, मेरा ही ज़ुल्म!"),
    _l("capture", "female", ["balanced"], "Business as usual. Koi personal baat nahi thi.",
       "बिज़नेस ऐज़ यूज़ुअल। कोई पर्सनल बात नहीं थी।"),
    _l("capture", "child", [], "Cut cut cut! Tum ghar jao, main aage badhunga!",
       "काट काट काट! तुम घर जाओ, मैं आगे बढ़ूँगा!"),
    _l("capture", "child", ["excited"], "Yayyy! Tumhari goli ghar gayi! Bye bye!",
       "येईईई! तुम्हारी गोली घर गई! बाय बाय!"),
    _l("capture", "child", ["drama"], "Isi vyavastha ke khilaaf kranti hogi… tumhari goli gayi!",
       "इसी व्यवस्था के खिलाफ़ क्रांति होगी… तुम्हारी गोली गई!"),

    # ------------------------------------------------------------ captured (victim)
    _l("captured", "male", [], "Arre! Itni mehnat bekaar? Theek hai — badla lenge.",
       "अरे! इतनी मेहनत बेकार? ठीक है — बदला लेंगे।"),
    _l("captured", "male", ["overconfident"], "Galti se hua. Wapas aake dikhaunga asli game.",
       "गलती से हुआ। वापस आके दिखाऊँगा असली गेम।"),
    _l("captured", "male", ["drama"], "Naaaheeee! Meri goli! Mera sapna! Sab barbaad!",
       "नहीहीहीं! मेरी गोली! मेरा सपना! सब बर्बाद!"),
    _l("captured", "female", [], "Hai rabba! Chalo, dobara try karte hain.",
       "हाय रब्बा! चलो, दोबारा ट्राई करते हैं।"),
    _l("captured", "female", ["drama"], "Ye kya ho gaya! Poore game ki izzat daav pe lag gayi!",
       "ये क्या हो गया! पूरे गेम की इज़्ज़त दाँव पे लग गई!"),
    _l("captured", "female", ["sweet"], "Koi baat nahi ji. Ghar jaake rest karo, hum phir aayenge.",
       "कोई बात नहीं जी। घर जाके रेस्ट करो, हम फिर आएँगे।"),
    _l("captured", "child", [], "Oye! Not fair! Wapas aaunga main!",
       "ओए! नॉट फेयर! वापस आऊँगा मैं!"),
    _l("captured", "child", ["drama"], "Mummyyyy! Ye meri goli kaat diya!",
       "मम्मीईई! ये मेरी गोली काट दिया!"),
    _l("captured", "child", ["excited"], "Gaya kaata! Chalo, phir se bhejenge — double speed!",
       "गया काटा! चलो, फिर से भेजेंगे — डबल स्पीड!"),

    # ------------------------------------------------------------ void (three sixes)
    _l("void", "male", [], "Teen chhah… aur sab vanish? Kismat ka sense of humour mast hai.",
       "तीन छह… और सब गायब? किस्मत का सेंस ऑफ ह्यूमर मस्त है।"),
    _l("void", "male", ["drama"], "Teen chhah! TEEN CHHAH! Aur kaam zero! Ye kaisa twist hai bhai?",
       "तीन छह! तीन छह! और काम ज़ीरो! ये कैसा ट्विस्ट है भाई?"),
    _l("void", "male", ["overconfident"], "Rule book ne galti ki hai. Main toh abhi bhi aage hoon.",
       "रूल बुक ने गलती की है। मैं तो अभी भी आगे हूँ।"),
    _l("void", "female", [], "Teen chhah aur kaam zero — director sahab, ye screenplay theek nahi hai.",
       "तीन छह और काम ज़ीरो — डायरेक्टर साहब, ये स्क्रीनप्ले ठीक नहीं है।"),
    _l("void", "female", ["drama"], "Oh no no no! Teen chhah gayab?! Main is scene se khush nahi hoon!",
       "ओह नो नो नो! तीन छह गायब?! मैं इस सीन से खुश नहीं हूँ!"),
    _l("void", "female", ["sweet"], "Chalo, achhi baat hai — doosron ko bhi mauka milega ji.",
       "चलो, अच्छी बात है — दूसरों को भी मौका मिलेगा जी।"),
    _l("void", "child", [], "Sab… gayab?! Ek minute pehle sab badhiya tha!",
       "सब… गायब?! एक मिनट पहले सब बढ़िया था!"),
    _l("void", "child", ["drama"], "Ye game khelne layak nahi hai! Main complain karunga!",
       "ये गेम खेलने लायक नहीं है! मैं कंप्लेन करूँगा!"),

    # ------------------------------------------------------------ nomove
    _l("nomove", "male", [], "Move hi nahi? Chalo, chai break.",
       "मूव ही नहीं? चलो, चाय ब्रेक।"),
    _l("nomove", "male", ["overconfident"], "Main strategy banata hoon, aur patta khali? Unbelievable.",
       "मैं स्ट्रैटजी बनाता हूँ, और पत्ता खाली? अनबिलीवेबल।"),
    _l("nomove", "female", [], "Koi move nahi? Theek hai. Patience sab kuch hai.",
       "कोई मूव नहीं? ठीक है। पेशेंस सब कुछ है।"),
    _l("nomove", "female", ["drama"], "Ye turn officially cancelled samajhiye. Aage badhiye.",
       "ये टर्न ऑफिशियली कैंसल्ड समझिए। आगे बढ़िए।"),
    _l("nomove", "child", [], "Kuch nahi chala? Okie — next time double speed!",
       "कुछ नहीं चला? ओके — नेक्स्ट टाइम डबल स्पीड!"),
    _l("nomove", "child", ["excited"], "Koi baat nahi! Main toh bas thoda rest kar raha tha!",
       "कोई बात नहीं! मैं तो बस थोड़ा रेस्ट कर रहा था!"),

    # ------------------------------------------------------------ finish (token home)
    _l("finish", "male", [], "Ek ghar pahuncha. Teen bache. Hisaab barabar hai.",
       "एक घर पहुँचा। तीन बचे। हिसाब बराबर है।"),
    _l("finish", "male", ["overconfident"], "Dekho aur seekho — aisa hota hai finish.",
       "देखो और सीखो — ऐसा होता है फिनिश।"),
    _l("finish", "female", [], "Ek pahunch gayi! Ab bas thoda aur dhyan se.",
       "एक पहुँच गई! अब बस थोड़ा और ध्यान से।"),
    _l("finish", "female", ["sweet"], "Wah! Ghar ki mandir mein aa gayi meri goli. Pranam.",
       "वाह! घर के मंदिर में आ गई मेरी गोली। प्रणाम।"),
    _l("finish", "child", [], "Ghar aa gaya! Mummy, dekho — main jeet raha hoon!",
       "घर आ गया! मम्मी, देखो — मैं जीत रहा हूँ!"),
    _l("finish", "child", ["excited"], "Home run! Ek down, teen to go!",
       "होम रन! एक डाउन, थ्री टू गो!"),

    # ------------------------------------------------------------ place (finished, not 1st)
    _l("place", "male", [], "Doosra number? Acceptable. Top class ke bahar kuch nahi hota.",
       "दूसरा नंबर? एक्सेप्टेबल। टॉप क्लास के बाहर कुछ नहीं होता।"),
    _l("place", "male", ["drama"], "Itne kareeb… ITNE KAREEB… aur phir bhi doosra! Kismat, tu bhi na!",
       "इतने क़रीब… इतने क़रीब… और फिर भी दूसरा! किस्मत, तू भी ना!"),
    _l("place", "female", [], "Achhi jagah hai. Upar wale ko manzoor tha itna hi aaj.",
       "अच्छी जगह है। ऊपर वाले को मंज़ूर था इतना ही आज।"),
    _l("place", "female", ["sweet"], "Runner-up bhi bahut achha hai ji. Mithai sabko milegi!",
       "रनर-अप भी बहुत अच्छा है जी। मिठाई सबको मिलेगी!"),
    _l("place", "child", [], "Number do? Almost winner bolte hain usko!",
       "नंबर दो? ऑलमोस्ट विनर बोलते हैं उसको!"),
    _l("place", "child", ["excited"], "Doosra bhi jeet jaisa hai! Star main hoon!",
       "दूसरा भी जीत जैसा है! स्टार मैं हूँ!"),

    # ------------------------------------------------------------ victory
    _l("victory", "male", [], "Aaj ka champion main hoon. Kal kaun? Dekha jayega.",
       "आज का चैंपियन मैं हूँ। कल कौन? देखा जाएगा।"),
    _l("victory", "male", ["overconfident"], "Bolo? Kaun bola tha pehle? MAIN bola tha pehle!",
       "बोलो? कौन बोला था पहले? मैं बोला था पहले!"),
    _l("victory", "male", ["drama"], "VICTORY! History likhi gayi — is board par, in haathon se!",
       "विक्ट्री! हिस्ट्री लिखी गई — इस बोर्ड पर, इन हाथों से!"),
    _l("victory", "female", [], "Jeet gayi! Ab sab ke liye mithai meri taraf se!",
       "जीत गई! अब सब के लिए मिठाई मेरी तरफ़ से!"),
    _l("victory", "female", ["drama"], "Aaj ki episode ka climax: rani jeet gayi! The End — happy ending!",
       "आज के एपिसोड का क्लाइमैक्स: रानी जीत गई! द एंड — हैप्पी एंडिंग!"),
    _l("victory", "female", ["sweet"], "Sab ne achha khela. Par aaj mera din tha, sach much!",
       "सब ने अच्छा खेला। पर आज मेरा दिन था, सच मुच्छ!"),
    _l("victory", "child", [], "MAIN JEETA! Ice cream time!",
       "मैं जीता! आइस क्रीम टाइम!"),
    _l("victory", "child", ["excited"], "Winner winner! Aaj dinner meri taraf se… mummy se puch ke!",
       "विनर विनर! आज डिनर मेरी तरफ़ से… मम्मी से पूछ के!"),

    # ------------------------------------------------------------ defeat
    _l("defeat", "male", [], "Aaj aapki, kal hamari. Revanche fixed hai.",
       "आज आपकी, कल हमारी। रिवांश फिक्स्ड है।"),
    _l("defeat", "male", ["drama"], "Main haar nahi maanta. Main bas… aaj ke liye ke maan gaya.",
       "मैं हार नहीं मानता। मैं बस… आज के लिए के मान गया।"),
    _l("defeat", "female", [], "Achha khela aapne. Next game ki taiyari shuru!",
       "अच्छा खेला आपने। नेक्स्ट गेम की तैयारी शुरू!"),
    _l("defeat", "female", ["sweet"], "Aap jeete, dil se badhai. Par agli baar dhyan rakhna ji!",
       "आप जीते, दिल से बधाई। पर अगली बार ध्यान रखना जी!"),
    _l("defeat", "child", [], "Thoda sa hi haar gaya main. Next time full jeetunga!",
       "थोड़ा सा ही हार गया मैं। नेक्स्ट टाइम फुल जीतूँगा!"),
    _l("defeat", "child", ["drama"], "Bas! Aaj mood kharab hai. Kal dekhte hain sab.",
       "बस! आज मूड खराब है। कल देखते हैं सब।"),

    # ------------------------------------------------------------ leave (reserved)
    _l("leave", "male", [], "Ja rahe ho? Theek hai — game yaad rakhenge.",
       "जा रहे हो? ठीक है — गेम याद रखेंगे।"),
    _l("leave", "male", ["drama"], "Aur ek yoddha chhod ke gaya… itihaas unhe yaad rakhega.",
       "और एक योद्धा छोड़ के गया… इतिहास उन्हें याद रखेगा।"),
    _l("leave", "female", [], "Jaldi se aana ji, hum log yahin hain.",
       "जल्दी से आना जी, हम लोग यहीं हैं।"),
    _l("leave", "female", ["sweet"], "Sambhal ke jaiye ji. Yahan sab theek rahega.",
       "संभल के जाइए जी। यहाँ सब ठीक रहेगा।"),
    _l("leave", "child", [], "Bye bye! Jaldi aana — promise?",
       "बाय बाय! जल्दी आना — प्रॉमिस?"),
    _l("leave", "child", ["excited"], "Arre ruko! Last roll toh karke jao!",
       "अरे रुको! लास्ट रोल तो करके जाओ!"),
]

# POOLS[(context, category)] -> tuple of lines (canonical order).
POOLS: Dict[Tuple[str, str], Tuple[Line, ...]] = {}
for _ctx in CONTEXTS:
    for _cat in CATEGORIES:
        _sel = tuple(l for l in LINES if l.context == _ctx and l.category == _cat)
        if not _sel:
            raise RuntimeError("comedy pool empty: %s/%s" % (_ctx, _cat))
        POOLS[(_ctx, _cat)] = _sel


# ---------------------------------------------------------------- rng

class Rng:
    """splitmix64 — mirrored exactly by the C# ComedyDirector.Rng so the two
    implementations produce identical selections from identical call
    sequences. Golden values are anchored in tests/test_comedy.py."""

    __slots__ = ("s",)

    def __init__(self, seed: int):
        self.s = seed & MASK

    def next(self) -> int:
        self.s = (self.s + 0x9E3779B97F4A7C15) & MASK
        z = self.s
        z = ((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9) & MASK
        z = ((z ^ (z >> 27)) * 0x94D049BB133111EB) & MASK
        return z ^ (z >> 31)

    def next_int(self, n: int) -> int:
        return self.next() % n

    def next_float(self) -> float:
        return self.next() / 18446744073709551616.0


# ---------------------------------------------------------------- director

class Reaction:
    """One comedy reaction: which seat speaks, in what context, with which
    R1 emotion tag, anchored to which token (-1 = none/any)."""

    __slots__ = ("seat_index", "context", "emotion", "token_index")

    def __init__(self, seat_index: int, context: str, emotion: str, token_index: int = -1):
        self.seat_index = seat_index
        self.context = context
        self.emotion = emotion
        self.token_index = token_index


class ComedyDirector:
    """Selects dialogue lines deterministically: category routing (A3),
    personality bias, no immediate repeats, per-(seat, context) cooldown.
    `now` is caller-supplied seconds (testable pure function).

    No-repeat semantics: history is the seat's recent DISTINCT line ids
    (appending a repeat of the tail is a no-op) and only the TAIL is
    excluded from re-pick — i.e. never the same line twice in a row.
    (A multi-id exclusion window was tried and REJECTED: it interacts with
    the personality bias by parking the preferred line inside the window,
    inverting the bias on small pools — 25% instead of ~80%. Enrichment,
    not exhaustion, is the correct behaviour; large M4 pools provide
    variety naturally.)"""

    HISTORY_PER_SEAT = 8   # remembered distinct ids

    def __init__(self, seed: int):
        self._rng = Rng(seed)
        self._history: Dict[int, List[str]] = {}
        self._last_at: Dict[Tuple[int, str], float] = {}

    def select(self, seat_index: int, context: str, category: str,
               personality: str, now: float, cooldown: float = 6.0) -> Optional[Line]:
        if context not in EMOTIONS:
            return None
        if context not in NO_COOLDOWN_CONTEXTS:
            last = self._last_at.get((seat_index, context))
            if last is not None and now - last < cooldown:
                return None

        cands = list(POOLS[(context, category)])
        hist = self._history.get(seat_index, [])
        tail = hist[-1] if hist else None
        fresh = [l for l in cands if l.id != tail]
        pool = fresh if fresh else cands

        preferred = [l for l in pool if personality in l.personalities]
        if preferred and len(pool) > 1 and self._rng.next_float() < 0.75:
            pool = preferred

        line = pool[self._rng.next_int(len(pool))]
        if not hist or hist[-1] != line.id:
            hist = hist + [line.id]
            self._history[seat_index] = hist[-self.HISTORY_PER_SEAT:]
        self._last_at[(seat_index, context)] = now
        return line


# ---------------------------------------------------------------- event mapping

def reactions_for_event(event: GameEvent, seats_in_play) -> List[Reaction]:
    """GameEvent -> comedy reactions (may be 0, 1 or 2). Mirrored by the C#
    ComedyEvents.FromGameEvent. Ordinary moves and turn ends are silent by
    design (anti-chatter)."""
    order = list(seats_in_play)

    def seat_index(seat) -> int:
        return order.index(seat)

    if isinstance(event, DiceRolled):
        if event.die == 6:
            return [Reaction(seat_index(event.seat), "six", EMOTIONS["six"])]
        if event.was_extra:
            return [Reaction(seat_index(event.seat), "extra", EMOTIONS["extra"])]
        return []
    if isinstance(event, NoLegalMove):
        return [Reaction(seat_index(event.seat), "nomove", EMOTIONS["nomove"])]
    if isinstance(event, TokenMoved):
        if event.is_release:
            return [Reaction(seat_index(event.seat), "release", EMOTIONS["release"], event.token_index)]
        return []
    if isinstance(event, TokenCaptured):
        return [
            Reaction(seat_index(event.attacker_seat), "capture", EMOTIONS["capture"], event.attacker_index),
            Reaction(seat_index(event.victim_seat), "captured", EMOTIONS["captured"], event.victim_index),
        ]
    if isinstance(event, TokenFinished):
        return [Reaction(seat_index(event.seat), "finish", EMOTIONS["finish"], event.token_index)]
    if isinstance(event, PlayerFinished):
        ctx = "victory" if event.place == 1 else "place"
        return [Reaction(seat_index(event.seat), ctx, EMOTIONS[ctx])]
    if isinstance(event, TurnVoided):
        return [Reaction(seat_index(event.seat), "void", EMOTIONS["void"])]
    if isinstance(event, MatchEnded):
        out: List[Reaction] = []
        winner = event.standings[0].seat if event.standings else None
        for s in order:
            if s == winner:
                out.append(Reaction(seat_index(s), "victory", EMOTIONS["victory"]))
            else:
                out.append(Reaction(seat_index(s), "defeat", EMOTIONS["defeat"]))
        return out
    # PlayerLeft / TurnEnded / TurnStarted: silent in V1 (Leave pool reserved).
    return []
