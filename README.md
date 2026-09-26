# Intermediate Project III

> 🎓 Video Game Project — Master's Degree in Video Game Programming  
> Developed as a team project at **The Core School**

**Intermediate Project III** is a video game developed in **Unity** as part of the Master's Degree in Video Game Programming at The Core School.

The project was developed collaboratively, with each team member being responsible for different gameplay and technical systems.

This repository is shared as part of my game development portfolio.

---

## 🎮 About the Project

Intermediate Project III is a gameplay-focused project developed using Unity and C#.

The game includes different gameplay systems created collaboratively by the development team, including:

- Player gameplay
- Enemy encounters
- Enemy AI
- Enemy spawning
- Reusable gameplay systems
- Data-driven configuration using Scriptable Objects

---

# 👨‍💻 My Contributions

My main work on this project focused on **enemy systems and gameplay programming using Unity and C#**.

I was responsible for:

- Enemy AI
- Enemy behavior
- Enemy state management
- Enemy spawning
- Enemy Object Pool / Spawn Pool
- Scriptable Objects
- Integration of enemy data with gameplay systems

---

## 🤖 Enemy AI

I implemented the **enemy artificial intelligence and behavior systems**.

The AI controls how enemies react and behave during gameplay.

My work included designing logic for different enemy actions and managing transitions between behaviors depending on the current gameplay situation.

This allowed enemies to react dynamically instead of relying on a single fixed behavior.

---

## 🧠 Enemy Behaviors

I developed the logic responsible for controlling enemy behavior.

Depending on the state of the enemy and the gameplay conditions, enemies could execute different actions.

The system was designed to keep enemy logic organized and make behaviors easier to maintain and extend.

This gave me practical experience working with:

- Enemy states
- Behavioral logic
- State transitions
- Gameplay conditions
- AI decision making

---

## ♻️ Enemy Spawn Pool

I implemented an **Object Pooling / Spawn Pool system for enemies**.

Instead of continuously creating and destroying enemy GameObjects, the system maintains reusable enemy instances.

The basic workflow is:

1. Enemy instances are prepared and stored in the pool.
2. When the game requires an enemy, an available instance is retrieved.
3. The enemy is positioned and activated.
4. The enemy performs its gameplay behavior.
5. When the enemy is defeated or no longer required, it is deactivated.
6. The instance returns to the pool and becomes available for reuse.

This approach reduces repeated GameObject instantiation and destruction during gameplay.

---

## 📦 Scriptable Objects

I used **Unity Scriptable Objects** to separate gameplay data from gameplay logic.

Scriptable Objects were used to store and configure data required by different systems.

This approach helped make the project:

- More modular
- Easier to configure
- Easier to maintain
- More reusable
- Less dependent on hard-coded values

It also allowed gameplay values to be modified from the Unity Editor without changing the underlying code.

---

## ⚙️ Technical Focus

My work on this project focused mainly on:

- C# gameplay programming
- Enemy AI
- Enemy behavior systems
- Object Pooling
- Spawn management
- Scriptable Objects
- Data-driven design
- GameObject lifecycle management
- Modular gameplay architecture

---

## 🛠️ Technologies

The project was developed using:

- **Unity**
- **C#**
- Scriptable Objects
- Unity Gameplay Systems
- Git / Version Control

---

## 🎯 What I Learned

This project gave me practical experience designing enemy systems and organizing gameplay logic in Unity.

Implementing enemy AI helped me improve my understanding of:

- State-based behavior
- Enemy decision making
- Gameplay conditions
- Modular AI architecture

The Object Pool system also gave me experience managing reusable GameObjects efficiently.

Using Scriptable Objects helped me understand how data-driven systems can improve maintainability and scalability in Unity projects.

---

## 👥 Collaborative Development

Intermediate Project III was developed as a **team project**.

Different members of the team worked on different areas of the game.

My individual programming contributions focused on:

**Enemy AI → Enemy Behaviors → Enemy Spawn Pool → Scriptable Objects**

Other systems and content in the project were developed collaboratively by other members of the team.

---

## 📌 Portfolio Notice

This repository is shared for **portfolio and educational purposes**.

Intermediate Project III was developed collaboratively as part of the Master's Degree in Video Game Programming at The Core School.

Not all assets, systems, or gameplay mechanics contained in this repository were created by me.

My personal contributions focused primarily on:

- **Enemy AI**
- **Enemy behavior systems**
- **Enemy Spawn Pool / Object Pool**
- **Scriptable Objects**
