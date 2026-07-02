//
//  ContentView.swift
//  AppleVisionPro
//
//  Created by TJ on 7/2/26.
//

import SwiftUI
import RealityKit

struct ContentView: View {
    @Environment(AppModel.self) private var appModel
    @Environment(\.openImmersiveSpace) private var openImmersiveSpace
    @Environment(\.dismissWindow) private var dismissWindow

    var body: some View {
        VStack {
            Spacer()

            VStack(spacing: 16) {
                Text("Vision Pro Eye Tracking")
                    .font(.title2)
                    .fontWeight(.bold)

                Button(action: {
                    Task {
                        await openImmersiveSpace(id: appModel.immersiveSpaceID)
                        dismissWindow()
                    }
                }) {
                    Text("Start Experience")
                        .font(.headline)
                        .frame(maxWidth: .infinity)
                        .padding()
                        .background(Color.blue)
                        .foregroundColor(.white)
                        .cornerRadius(8)
                }
            }
            .padding()

            Spacer()
        }
    }
}

#Preview(windowStyle: .automatic) {
    ContentView()
        .environment(AppModel())
}
