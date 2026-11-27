// Copyright 2019 DeepMind Technologies Limited
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Linq;
using System.Xml;
using UnityEngine;

namespace Mujoco {

// A facade that allows to select a type of Mujoco shape and define that shape's properties.
//
// To work around the lack of serialization support for polymorphism, the class aggregates
// the settings of all available shape types. Coupled with a set of custom property drawers,
// it will be presented in the Inspector as a single entity, encapsulating the details of
// data persistance, Mjcf parsing and generation and debug visualization.
public abstract class MjShapeComponent : MjComponent {

  public enum ShapeTypes {
    Sphere,
    Capsule,
    Ellipsoid,
    Cylinder,
    Box,
    Plane,
    Mesh,
    HField
  }

  public ShapeTypes ShapeType;

  public MjSphereShape Sphere = new MjSphereShape();
  public MjCapsuleShape Capsule = new MjCapsuleShape();
  public MjCylinderShape Cylinder = new MjCylinderShape();
  public MjBoxShape Box = new MjBoxShape();
  public MjEllipsoidShape Ellipsoid = new MjEllipsoidShape();
  public MjPlaneShape Plane = new MjPlaneShape();
  public MjMeshShape Mesh = new MjMeshShape();
  public MjHeightFieldShape HField = new MjHeightFieldShape();

  public IMjShape Shape {
    get {
      switch (ShapeType) {
        default:
        case ShapeTypes.Sphere: return Sphere;
        case ShapeTypes.Capsule: return Capsule;
        case ShapeTypes.Cylinder: return Cylinder;
        case ShapeTypes.Ellipsoid: return Ellipsoid;
        case ShapeTypes.Box: return Box;
        case ShapeTypes.Plane: return Plane;
        case ShapeTypes.Mesh: return Mesh;
        case ShapeTypes.HField: return HField;
      }
    }
  }

  public void ShapeToMjcf(XmlElement mjcf, Transform transform) {
    mjcf.SetAttribute("type", ShapeType.ToString().ToLower());
    Shape.ToMjcf(mjcf, transform);
  }

  public void ShapeFromMjcf(XmlElement mjcf) {
    if (mjcf.GetStringAttribute("type", "") == "cone") {
      ConeFromMjcf(mjcf);
      return;
    }
    ShapeType = mjcf.GetEnumAttribute<ShapeTypes>(
        "type", ShapeTypes.Sphere, ignoreCase: true);
    Shape.FromMjcf(mjcf);
  }

  // Unity has no cone shape, and a mesh shape needs a mesh asset: a cone or conical frustum is
  // replaced by the cylinder with its mean radius, which changes its shape, mass and contacts.
  private void ConeFromMjcf(XmlElement mjcf) {
    ShapeType = ShapeTypes.Cylinder;
    Vector3 fromPoint, toPoint;
    var fromto = MjEngineTool.ParseFromToMjcf(mjcf, out fromPoint, out toPoint);

    // size: radius at -z, half-height, radius at +z; with fromto: radius at from, radius at to
    var size = mjcf.GetFloatArrayAttribute("size", defaultValue: new float[] { 0.5f, 1.0f, 0.0f });
    if (fromto) {
      Cylinder.Radius = 0.5f * (size[0] + size[1]);
      Cylinder.HalfHeight = (toPoint - fromPoint).magnitude * 0.5f;
    } else {
      Cylinder.Radius = 0.5f * (size[0] + size[2]);
      Cylinder.HalfHeight = size[1];
    }
    Debug.LogError(
        $"{mjcf.GetStringAttribute("name", mjcf.Name)}: cone geoms are not supported in Unity. " +
        $"The cone is replaced by a cylinder with its mean radius {Cylinder.Radius}, which " +
        "changes its shape, mass and contacts.");
  }

  public Tuple<Vector3[], int[]> BuildMesh() {
    return Shape.BuildMesh();
  }

  public Vector4 GetChangeStamp() {
    var stamp = Shape.GetChangeStamp();
    stamp.w = (float)ShapeType;
    return stamp;
  }

  public void DrawGizmos(Transform transform) {
    var oldMatrix = Gizmos.matrix;
    Shape.DebugDraw(transform);
    Gizmos.matrix = oldMatrix;
  }
}
}
