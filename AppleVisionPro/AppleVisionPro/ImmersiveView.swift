//
//  ImmersiveView.swift
//  AppleVisionPro
//
//  Created by TJ on 7/2/26.
//

import SwiftUI
import RealityKit
import RealityKitContent

struct ImmersiveView: View {
    @State private var eyeTrackingModel = EyeTrackingModel()
    @State private var sphereEntities: [ModelEntity] = []
    @State private var spherePositions: [SIMD3<Float>] = []
    @State private var highlightedIndex: Int? = nil

    var body: some View {
        ZStack {
            RealityView { content in
                let rootAnchor = AnchorEntity(world: .zero)

                // Add lighting above user
                var dirLight = DirectionalLight()
                dirLight.light.intensity = 1500
                let lightAnchor = AnchorEntity(world: [0, 1.5, 0])
                lightAnchor.addChild(dirLight)
                rootAnchor.addChild(lightAnchor)

                // Positions: -0.5, 0.0, +0.5 distributed on X axis
                let positions: [SIMD3<Float>] = [
                    [-0.5, 1.3, -1.5],
                    [0.0, 1.3, -1.5],
                    [0.5, 1.3, -1.5]
                ]

                var localSpheres: [ModelEntity] = []
                for (index, position) in positions.enumerated() {
                    let mesh = MeshResource.generateSphere(radius: 0.15)
                    let material = SimpleMaterial(color: .gray, isMetallic: false)
                    let sphere = ModelEntity(mesh: mesh, materials: [material])
                    sphere.position = position
                    sphere.name = "Target_\(index)"

                    // Required components for system input & hover tracking
                    sphere.components.set(InputTargetComponent())
                    sphere.components.set(HoverEffectComponent())
                    sphere.components.set(CollisionComponent(shapes: [.generateSphere(radius: 0.15)]))

                    rootAnchor.addChild(sphere)
                    localSpheres.append(sphere)
                }

                DispatchQueue.main.async {
                    sphereEntities = localSpheres
                    spherePositions = positions
                }

                content.add(rootAnchor)
            } update: { content in
                // Update colors based on pinch state
                for (index, sphere) in sphereEntities.enumerated() {
                    let isHighlighted = index == highlightedIndex
                    let color: UIColor = isHighlighted ? .green : .gray
                    var material = SimpleMaterial(color: color, isMetallic: false)
                    sphere.model?.materials = [material]
                }
            }
            // Pinch to highlight sphere (stays green until you pinch another)
            .gesture(
                SpatialTapGesture()
                    .targetedToAnyEntity()
                    .onEnded { value in
                        if let indexStr = value.entity.name.split(separator: "_").last,
                           let index = Int(indexStr) {
                            highlightedIndex = index
                            eyeTrackingModel.updateGazedObject(name: value.entity.name)
                            print("Pinched: \(value.entity.name)")
                        }
                    }
            )

            // Overlay status display
            VStack(alignment: .leading, spacing: 8) {
                Text("Vision Pro Eye Tracking Demo")
                    .font(.system(size: 16, weight: .bold))
                    .foregroundColor(.white)

                Divider()
                    .background(Color.white.opacity(0.3))

                Text("Currently Gazing At:")
                    .font(.system(size: 12))
                    .foregroundColor(.white)

                Text(eyeTrackingModel.gazedObjectName)
                    .font(.system(size: 14, weight: .semibold, design: .monospaced))
                    .foregroundColor(eyeTrackingModel.isGazingAtObject ? .green : .red)
            }
            .padding(12)
            .background(Color.black.opacity(0.7))
            .cornerRadius(8)
            .padding()
            .frame(maxHeight: .infinity, alignment: .topLeading)
        }
    }
}

#Preview(immersionStyle: .full) {
    ImmersiveView()
        .environment(AppModel())
}
