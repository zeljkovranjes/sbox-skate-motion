using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Vehicle;

var steer = args.Length > 0 ? float.Parse(args[0], CultureInfo.InvariantCulture) : 0.5f;
var friction = args.Length > 1 ? float.Parse(args[1], CultureInfo.InvariantCulture) : 0.85f;
var initialSpeed = args.Length > 2 ? float.Parse(args[2], CultureInfo.InvariantCulture) : 5.675f;
var frequency = args.Length > 3 ? int.Parse(args[3], CultureInfo.InvariantCulture) : 240;
var step = 1f / frequency;
var manual = args.Length > 4 ? float.Parse(args[4], CultureInfo.InvariantCulture) : 0f;
var settings = new SkateSettings();
const float radius = 0.0285f;
var origins = new Vector3[4];
for (var i = 0; i < 4; i++) origins[i] = SkateWheels.Offset(i, settings) - Vector3.UnitZ * (settings.RideHeight - radius);
var board = new ArticulatedBoard(settings, origins, radius);
var pilot = new BoardPilot(settings);
board.Reset(Vector3.UnitZ * settings.RideHeight, Quaternion.Identity, Vector3.UnitX * initialSpeed, Vector3.Zero);
board.Body(7) = new DynamicBody { Rotation = Quaternion.Identity };
var contacts = new CollisionContact[4];
var timer = new Stopwatch();
Console.WriteLine("time,speed,target,front,rear,yaw,wheelspin,gap,energy,axleerror,slip,pitch,wheels,mode");
for (var tick = 0; tick < frequency * 4; tick++)
{
    var input = tick >= frequency / 2 && tick < frequency * 3 / 2 ? new ControlInput(Steer: steer) : default;
    if (manual != 0) input = tick >= frequency / 2 && tick < frequency * 5 / 2 ? new ControlInput(FlickY: manual) : default;
    var target = input.Steer * settings.TruckAngle / (1f + board.Deck.Velocity.Length() * settings.SteeringSpeedScale);
    board.BeginStep();
    for (var wheel = 0; wheel < 4; wheel++)
    {
        ref var body = ref board.Body(wheel + 3);
        var point = body.Position - Vector3.UnitZ * radius;
        var axle = Vector3.Transform(Vector3.UnitY, body.Rotation);
        var tangent = Vector3.Normalize(Vector3.Cross(axle, Vector3.UnitZ));
        contacts[wheel] = new(wheel + 3, 7, point, new Vector3(point.X, point.Y, 0), Vector3.UnitZ,
            tangent, Vector3.Cross(Vector3.UnitZ,tangent), friction, friction*0.8f, 0);
    }
    var result = pilot.Apply(board, contacts, input, step);
    if (tick >= frequency / 2) timer.Start();
    board.Step(contacts, target, 0, step);
    timer.Stop();
    if (tick % (frequency / 8) == 0)
    {
        var a = Vector3.Transform(Vector3.UnitX, Quaternion.Conjugate(board.Body(0).Rotation)*board.Body(1).Rotation);
        var b = Vector3.Transform(Vector3.UnitX, Quaternion.Conjugate(board.Body(0).Rotation)*board.Body(2).Rotation);
        var spin = Vector3.Dot(board.Body(3).AngularVelocity, Vector3.Transform(Vector3.UnitY,board.Body(3).Rotation));
        var axleError = MathF.Acos(Math.Clamp(Vector3.Dot(Vector3.Transform(Vector3.UnitY,board.Body(3).Rotation),Vector3.Transform(Vector3.UnitY,board.Body(1).Rotation)),-1,1));
        var pointVelocity = board.Body(3).Velocity + Vector3.Cross(board.Body(3).AngularVelocity,-Vector3.UnitZ*radius);
        var slip = new Vector2(pointVelocity.X, pointVelocity.Y).Length();
        Console.WriteLine(FormattableString.Invariant($"{tick*step:F3},{board.Deck.Velocity.Length():F6},{target:F6},{MathF.Atan2(a.Y,a.X):F6},{MathF.Atan2(b.Y,b.X):F6},{board.Deck.AngularVelocity.Z:F6},{spin:F6},{board.MaximumAnchorError():F6},{board.KineticEnergy():F6},{axleError:F6},{slip:F6},{MathF.Asin(board.Deck.Forward.Z):F6},{result.WheelCount},{result.Mode}"));
    }
}
Console.Error.WriteLine($"Core solver mean: {timer.Elapsed.TotalMilliseconds / (frequency*3.5):F4} ms/substep. Scene queries, pilot, telemetry and engine integration excluded.");
