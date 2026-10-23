"""Generate original, deterministic mono WAV cues and seamless minigame loops.
Run from repository root: python Tools/Audio/generate_minigame_audio.py
"""
from array import array
from pathlib import Path
import math, random, wave

ROOT = Path(__file__).resolve().parents[2]
RATE = 22050
TAU = 2 * math.pi

def buffer(seconds): return array('d', [0.0]) * int(round(seconds * RATE))
def hz(midi): return 440.0 * 2 ** ((midi - 69) / 12)
def note(data, at, midi, seconds, volume=.18, kind='bell', wrap=False):
    frequency = hz(midi)
    start = int(round(at * RATE))
    count = max(1, int(seconds * RATE))
    for i in range(count):
        t = i / RATE
        attack = min(1.0, t / .008)
        release = min(1.0, (count-i) / (RATE*.035))
        if kind == 'bass':
            sample = math.sin(TAU*frequency*t) + .12*math.sin(TAU*frequency*2*t)
            envelope = attack * release * math.exp(-t*4)
        elif kind == 'tick':
            sample = math.sin(TAU*frequency*t) + .38*math.sin(TAU*frequency*2.73*t)
            envelope = attack * release * math.exp(-t*65)
        elif kind == 'pad':
            sample = math.sin(TAU*frequency*t) + .12*math.sin(TAU*frequency*2*t)
            envelope = min(1.0,t/.12) * min(1.0,(count-i)/(RATE*.24)) * .45
        else:
            sample = math.sin(TAU*frequency*t) + .22*math.sin(TAU*frequency*2*t)*math.exp(-t*7)
            envelope = attack * release * math.exp(-t*5)
        pos = start+i
        if wrap: pos %= len(data)
        elif pos >= len(data): break
        data[pos] += sample * envelope * volume

def save(relative, data):
    peak = max(abs(x) for x in data) or 1
    gain = min(1.0,.84/peak)
    pcm = array('h', (round(max(-1,min(1,x*gain))*32767) for x in data))
    target = ROOT / relative
    target.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(target),'wb') as stream:
        stream.setnchannels(1);stream.setsampwidth(2);stream.setframerate(RATE)
        stream.writeframes(pcm.tobytes())
    print(relative,round(len(data)/RATE,2),'s', 'peak',round(peak*gain,3))

def cue(name, notes, kind='bell', duration=.6):
    data=buffer(duration)
    for at,midi,length,volume in notes: note(data,at,midi,length,volume,kind)
    save('Assets/Audio/SFX/'+name+'.wav',data)

cue('ui_reject',[(0,55,.14,.25),(.09,49,.19,.22)],duration=.32)
cue('minigame_start',[(0,60,.25,.21),(.12,64,.24,.20),(.24,67,.38,.24)],duration=.65)
cue('rush_queue',[(0,74,.12,.18),(.07,79,.16,.16)],duration=.26)
cue('rush_reorder',[(0,69,.1,.16),(.06,72,.13,.16)],duration=.22)
cue('order_ready',[(0,79,.3,.24),(.16,84,.35,.21)],duration=.55)
cue('combo_delivery',[(0,72,.28,.22),(.11,76,.3,.22),(.22,79,.36,.23)],duration=.62)
cue('combo_order_failed',[(0,64,.16,.2),(.13,57,.22,.2),(.26,52,.24,.17)],duration=.54)
cue('combo_assign',[(0,67,.1,.21),(.07,74,.18,.17)],duration=.28)
cue('combo_return',[(0,67,.24,.18),(.11,72,.27,.19)],duration=.42)
cue('combo_stage',[(0,76,.11,.11)],duration=.16)
cue('combo_washed',[(0,81,.16,.15),(.06,86,.19,.12)],duration=.29)
cue('combo_break_tick',[(0,89,.07,.27)],kind='tick',duration=.1)
cue('combo_warning',[(0,76,.12,.17),(.19,76,.14,.17)],duration=.38)

def music(name,bpm,mode):
    beat=60/bpm;bars=16;data=buffer(beat*4*bars)
    roots=[48,45,53,55]
    melodies={
      'energy':[72,None,76,None,79,None,76,None,74,None,72,None,69,None,67,None],
      'rush':[72,76,None,79,76,None,74,None,72,None,69,72,None,74,67,None],
      'combo':[72,None,74,76,None,79,None,76,74,None,72,69,None,67,None,69]
    }
    for bar in range(bars):
        root=roots[(bar//2)%4]
        for third in [0,3 if root==45 else 4,7]:
            note(data,bar*4*beat,root+third,beat*3.9,.045,'pad',True)
        for n in [0,2]:note(data,(bar*4+n)*beat,root-12,beat*.7,.15,'bass',True)
        if mode!='energy':
            for step in [1,3]:note(data,(bar*4+step)*beat,91, .07,.045,'tick',True)
        for step in range(4):
            value=melodies[mode][(bar*4+step)%16]
            if value is not None:
                # Deixa espaço entre as frases e evita ocupar todas as notas.
                if bar%4==3 and step>1:continue
                note(data,(bar*4+step)*beat,value,beat*.68,.095 if mode=='energy' else .11,'bell',True)
    save('Assets/Audio/Music/'+name+'.wav',data)

music('energy_station_loop',84,'energy')
music('rush_balance_loop',108,'rush')
music('combo_crew_loop',96,'combo')
