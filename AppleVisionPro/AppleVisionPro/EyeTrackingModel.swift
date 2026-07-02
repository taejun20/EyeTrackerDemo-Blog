//
//  EyeTrackingModel.swift
//  AppleVisionPro
//
//  Created by TJ on 7/2/26.
//

import Foundation

@MainActor
@Observable
class EyeTrackingModel {
    var gazedObjectName: String = "None"
    var isGazingAtObject: Bool = false

    func updateGazedObject(name: String?) {
        if let name = name {
            gazedObjectName = name
            isGazingAtObject = true
        } else {
            gazedObjectName = "None"
            isGazingAtObject = false
        }
    }
}
