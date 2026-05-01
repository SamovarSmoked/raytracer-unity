# 🔆 Unity GPU Ray Tracer

A custom real-time ray tracer built from scratch in Unity using **Compute Shaders (HLSL)**.  
Educational project focused on **GPU optimization** and **visual fidelity**.

![Unity](https://img.shields.io/badge/Unity-6-black?logo=unity)
![GPU](https://img.shields.io/badge/Rendering-Compute%20Shader-green)
![License](https://img.shields.io/badge/License-MIT-blue)

## ✨ Features

### Phase 1 — Foundation (current)
- Camera ray generation from inverse projection matrices
- Analytic sphere intersection (quadratic formula)
- Infinite ground plane with procedural checkerboard
- Blinn-Phong shading (ambient + diffuse + specular)
- Hard shadows via shadow rays
- Single-bounce reflections with Fresnel approximation
- Procedural gradient skybox with sun disc & glow
- Linear rendering with gamma correction

### Roadmap
- **Phase 2** — Dynamic scene via `StructuredBuffer<Sphere>`
- **Phase 3** — PBR materials (Lambertian, Metal, Dielectric)
- **Phase 4** — Path tracing with progressive accumulation & PRNG
- **Phase 5** — Camera controls, DoF, UI overlay
- **Phase 6** — BVH acceleration, mesh tracing, textures

## 🚀 Getting Started

### Prerequisites
- **Unity 6** (or 2022.3 LTS)
- GPU with compute shader support (any modern GPU)

### Setup
1. Clone this repository
2. Open the project folder with **Unity Hub** → **Open** → select `raytracer-unity/`
3. Unity will auto-generate `ProjectSettings/`, `Library/`, etc.
4. Create a new Scene (`File → New Scene → Basic Built-in`)
5. Select the **Main Camera** in the Hierarchy
6. Add the `RayTracingMaster` component (`Add Component → RayTracingMaster`)
7. Drag the `Assets/Shaders/RayTracingShader` compute shader into the **Compute Shader** field
8. Press **Play** ▶️

## 📂 Project Structure

```
Assets/
├── Scripts/
│   └── RayTracingMaster.cs        # CPU controller: dispatch, blit, parameters
└── Shaders/
    └── RayTracingShader.compute    # GPU ray tracing kernel (HLSL)
```

## 🧠 How It Works

```
┌─────────────┐      Camera matrices      ┌──────────────────┐
│    C# CPU    │ ────────────────────────► │   Compute Shader  │
│  Controller  │      Dispatch(8x8)        │      (GPU)        │
│              │ ◄──────────────────────── │                    │
└─────────────┘      RenderTexture         │  For each pixel:  │
       │                                    │  1. Generate ray  │
       ▼                                    │  2. Intersect     │
   Blit to                                  │  3. Shade         │
   Screen                                   │  4. Reflect       │
                                            │  5. Gamma correct │
                                            └──────────────────┘
```

## 📝 License

MIT — free to use for learning and portfolio purposes.
