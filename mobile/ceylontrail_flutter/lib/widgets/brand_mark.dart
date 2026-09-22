import 'package:flutter/material.dart';

import '../theme/app_theme.dart';

class BrandMark extends StatelessWidget {
  const BrandMark({this.size = 46, super.key});

  final double size;

  @override
  Widget build(BuildContext context) => Semantics(
    label: 'CeylonTrail leaf and route mark',
    image: true,
    child: CustomPaint(size: Size.square(size), painter: _BrandMarkPainter()),
  );
}

class _BrandMarkPainter extends CustomPainter {
  @override
  void paint(Canvas canvas, Size size) {
    final scale = size.width / 80;
    canvas.save();
    canvas.scale(scale);
    final leaf = Paint()..color = CeylonColors.forest;
    final vein = Paint()
      ..color = CeylonColors.tea
      ..style = PaintingStyle.stroke
      ..strokeWidth = 3
      ..strokeCap = StrokeCap.round;
    final shape = Path()
      ..moveTo(40, 70)
      ..cubicTo(17, 61, 10, 39, 20, 12)
      ..cubicTo(44, 16, 57, 31, 53, 50)
      ..cubicTo(51, 59, 46, 65, 40, 70)
      ..close();
    canvas.drawPath(shape, leaf);
    final route = Path()
      ..moveTo(25, 20)
      ..cubicTo(35, 33, 41, 48, 41, 65);
    canvas.drawPath(route, vein);
    canvas.restore();
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => false;
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
          Text(
            'CeylonTrail',
            style: Theme.of(context).textTheme.titleLarge?.copyWith(
              fontFamily: 'Playfair Display',
              fontSize: compact ? 19 : 22,
            ),
          ),
          Text(
            'INTELLIGENCE PLATFORM',
            style: Theme.of(context).textTheme.labelSmall?.copyWith(
              color: CeylonColors.tea,
              fontWeight: FontWeight.w800,
              letterSpacing: 1.2,
            ),
          ),
        ],
      ),
    ],
  );
}
