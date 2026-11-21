Interaction Technique Bake-Off

Week 4 practical for Virtual, Augmented and Spatial Computing (TUS). Student: Umedchon Khasanov, K00322179.

What it is

The same task built three ways in Unity: pick the highlighted cube from nine cubes on an arc and place it into a socket on a pedestal.

Variant	Input
A	Ray with trigger select
B	Direct grab (the hand must touch the cube)
C	Gaze + pinch (head gaze, because the Quest 2 has no eye tracking)

All three start from one main menu, where you also set the participant number. After ten placements a panel takes you back to the menu. A TrialManager script picks the target cube, times each placement and logs it to a CSV file.

Demo videos (Meta Quest 2)

Video link: https://www.youtube.com/playlist?list=PLfgW0IaW251g

Built with
Unity 6.3 (6000.3)
XR Interaction Toolkit 3.3.2
XR Hands
OpenXR (Meta Quest feature group)
Tested on Meta Quest 2 and in the Meta XR Simulator
Scenes
MainMenu
BakeOff_A_Ray
BakeOff_B_Direct
BakeOff_C_GazePinch

Only the rig and the variant setting differ between the three variant scenes.
