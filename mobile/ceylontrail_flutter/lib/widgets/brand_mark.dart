import 'package:flutter/material.dart';

import '../theme/app_theme.dart';

class BrandMark extends StatelessWidget {
  const BrandMark({this.size = 46, this.color = CeylonColors.forest, super.key});

  final double size;
  final Color color;

  @override
  Widget build(BuildContext context) => Semantics(
    label: 'CeylonTrail sun and trail mark',
    image: true,
    child: CustomPaint(size: Size.square(size), painter: _BrandMarkPainter(color)),
  );
}

class _BrandMarkPainter extends CustomPainter {
  const _BrandMarkPainter(this.color);
  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    final scale = size.width / 80;
    canvas.save();
    canvas.scale(scale);

    final inkPaint = Paint()..color = color;
    final accentColor = color.withValues(alpha: .55);
    final sunPaint = Paint()..color = color.withValues(alpha: .9);
    canvas.drawCircle(const Offset(56, 23), 8, sunPaint);

    final horizon = Path()
      ..moveTo(11, 50)
      ..cubicTo(24, 42, 39, 42, 54, 49)
      ..cubicTo(61, 52, 67, 52, 71, 50);
    final horizonPaint = Paint()
      ..color = color
      ..style = PaintingStyle.stroke
      ..strokeWidth = 3
      ..strokeCap = StrokeCap.round;
    canvas.drawPath(horizon, horizonPaint);

    final trail = Path()
      ..moveTo(16, 63)
      ..cubicTo(25, 55, 35, 55, 44, 60)
      ..cubicTo(52, 65, 58, 64, 64, 56);
    final trailPaint = Paint()
      ..color = color
      ..style = PaintingStyle.stroke
      ..strokeWidth = 5
      ..strokeCap = StrokeCap.round;
    canvas.drawPath(trail, trailPaint);

    final inner = Path()
      ..moveTo(25, 29)
      ..cubicTo(18, 34, 16, 42, 18, 49);
    final innerPaint = Paint()
      ..color = accentColor
      ..style = PaintingStyle.stroke
      ..strokeWidth = 2
      ..strokeCap = StrokeCap.round;
    canvas.drawPath(inner, innerPaint);
    canvas.drawCircle(const Offset(56, 23), 3, inkPaint);
    canvas.restore();
  }

  @override
  bool shouldRepaint(covariant _BrandMarkPainter oldDelegate) => oldDelegate.color != color;
}

class BrandLockup extends StatelessWidget {
  const BrandLockup({this.compact = false, super.key});

  final bool compact;

  @override
  Widget build(BuildContext context) => Row(
    mainAxisSize: MainAxisSize.min,
    children: [
      BrandMark(size: compact ? 36 : 44),
      const SizedBox(width: CeylonSpacing.sm),
      Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('CeylonTrail', style: Theme.of(context).textTheme.titleLarge?.copyWith(fontFamily: 'Playfair Display', fontSize: compact ? 19 : 22)),
          Text('TRAVEL WITH INTENTION', style: Theme.of(context).textTheme.labelSmall?.copyWith(color: CeylonColors.tea, fontWeight: FontWeight.w800, letterSpacing: 1.15)),
        ],
      ),
    ],
  );
}
