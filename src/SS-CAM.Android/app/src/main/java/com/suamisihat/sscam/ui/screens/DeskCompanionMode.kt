package com.suamisihat.sscam.ui.screens

import android.content.res.Configuration
import androidx.compose.animation.core.*
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.suamisihat.sscam.data.models.LiveTaskDto
import com.suamisihat.sscam.data.models.ProjectItem
import com.suamisihat.sscam.ui.theme.*
import kotlinx.coroutines.delay
import java.text.SimpleDateFormat
import java.util.*

/**
 * Ambient Studio Desk Companion Mode (Always-On Standby)
 * Inspired by Swiss watchmaking and Braun design principles:
 * - High-legibility Swiss typography digital clock & live date
 * - Tactile Pomodoro analog focus dial with phase indicators
 * - Next prayer time countdown ticker with audio chime status
 * - Current deliverable focus pill & live studio workstream ticker
 * - One-tap exit and ambient low-brightness OLED protector
 */
@Composable
fun DeskCompanionMode(
    activeProjects: List<ProjectItem> = emptyList(),
    liveTasks: List<LiveTaskDto> = emptyList(),
    onExit: () -> Unit
) {
    val configuration = LocalConfiguration.current
    val isLandscape = configuration.orientation == Configuration.ORIENTATION_LANDSCAPE

    // Live studio workstream telemetry
    val infiniteTransition = rememberInfiniteTransition(label = "pulse")
    val pulseAlpha by infiniteTransition.animateFloat(
        initialValue = 0.35f,
        targetValue = 1f,
        animationSpec = infiniteRepeatable(
            animation = tween(900, easing = FastOutSlowInEasing),
            repeatMode = RepeatMode.Reverse
        ),
        label = "pulseAlpha"
    )

    val activeWorkstations = remember(liveTasks) {
        liveTasks.filter {
            it.state.equals("working", ignoreCase = true) || it.state.equals("active", ignoreCase = true)
        }
    }
    val activeCount = if (activeWorkstations.isNotEmpty()) activeWorkstations.size else liveTasks.size
    val activeLead = activeWorkstations.firstOrNull() ?: liveTasks.firstOrNull()

    // Time State (Live ticking)
    var currentTime by remember { mutableStateOf(Calendar.getInstance().time) }
    LaunchedEffect(Unit) {
        while (true) {
            currentTime = Calendar.getInstance().time
            delay(1000L)
        }
    }

    val timeFormat = remember { SimpleDateFormat("HH:mm:ss", Locale.getDefault()) }
    val timeHourMin = remember { SimpleDateFormat("HH:mm", Locale.getDefault()) }
    val timeSec = remember { SimpleDateFormat("ss", Locale.getDefault()) }
    val dateFormat = remember { SimpleDateFormat("EEEE, d MMMM yyyy", Locale.getDefault()) }

    // Pomodoro Focus Timer State
    var isTimerRunning by remember { mutableStateOf(true) }
    var secondsRemaining by remember { mutableStateOf(25 * 60) }
    val totalSeconds = 25 * 60

    LaunchedEffect(isTimerRunning) {
        while (isTimerRunning && secondsRemaining > 0) {
            delay(1000L)
            secondsRemaining -= 1
        }
    }

    val pomoMinutes = secondsRemaining / 60
    val pomoSeconds = secondsRemaining % 60
    val pomoProgress = 1f - (secondsRemaining.toFloat() / totalSeconds.toFloat())

    // Active project highlight
    val currentFocusTask = activeProjects.firstOrNull { 
        !it.status.equals("done", true) && !it.status.equals("completed", true) 
    } ?: activeProjects.firstOrNull()

    // Ambient Backdrop
    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(Color(0xFF030712)) // Pure deep OLED black
            .padding(if (isLandscape) 24.dp else 16.dp)
    ) {
        // Ambient Subtle Sine Glow in center
        Canvas(modifier = Modifier.fillMaxSize()) {
            drawCircle(
                brush = Brush.radialGradient(
                    colors = listOf(
                        Color(0xFF0078D4).copy(alpha = 0.08f),
                        Color(0xFFD4AF37).copy(alpha = 0.03f),
                        Color.Transparent
                    ),
                    center = center,
                    radius = size.minDimension * 0.7f
                )
            )
        }

        // Top Controls: Exit & Status
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .align(Alignment.TopCenter),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Row(
                verticalAlignment = Alignment.CenterVertically,
                modifier = Modifier
                    .clip(RoundedCornerShape(20.dp))
                    .background(Color(0xFF111827))
                    .border(1.dp, Color(0xFF1F2937), RoundedCornerShape(20.dp))
                    .padding(horizontal = 12.dp, vertical = 6.dp)
            ) {
                Box(
                    modifier = Modifier
                        .size(8.dp)
                        .clip(CircleShape)
                        .background(if (activeLead != null) Color(0xFF10B981).copy(alpha = pulseAlpha) else Color(0xFF10B981))
                )
                Spacer(modifier = Modifier.width(8.dp))
                Text(
                    text = if (activeLead != null) {
                        "● $activeCount IN STUDIO • ${activeLead.designerName?.uppercase() ?: "DESIGNER"}: ${(activeLead.projectName ?: "WORKING").take(22)}"
                    } else {
                        "DESK STANDBY • LIVE NAS SYNC"
                    },
                    fontSize = 9.sp,
                    fontWeight = FontWeight.Bold,
                    color = if (activeLead != null) Color.White else Color(0xFF9CA3AF),
                    letterSpacing = 1.sp,
                    maxLines = 1
                )
            }

            IconButton(
                onClick = onExit,
                modifier = Modifier
                    .clip(CircleShape)
                    .background(Color(0xFF111827))
                    .border(1.dp, Color(0xFF1F2937), CircleShape)
                    .size(36.dp)
            ) {
                Icon(
                    Icons.Default.Close,
                    contentDescription = "Exit Desk Mode",
                    tint = Color.White,
                    modifier = Modifier.size(18.dp)
                )
            }
        }

        if (isLandscape) {
            // Landscape Layout: Dual Column (Left: Big Clock + Task, Right: Pomodoro Instrument)
            Row(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(top = 44.dp),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                // Left Column: Swiss Typography Clock & Active Sprint
                Column(
                    modifier = Modifier.weight(1.2f),
                    verticalArrangement = Arrangement.Center
                ) {
                    Row(verticalAlignment = Alignment.Bottom) {
                        Text(
                            text = timeHourMin.format(currentTime),
                            fontSize = 84.sp,
                            fontWeight = FontWeight.Light,
                            color = Color.White,
                            letterSpacing = (-4).sp,
                            lineHeight = 84.sp
                        )
                        Text(
                            text = ":${timeSec.format(currentTime)}",
                            fontSize = 32.sp,
                            fontWeight = FontWeight.Light,
                            color = Color(0xFF38BDF8),
                            modifier = Modifier.padding(bottom = 12.dp, start = 4.dp)
                        )
                    }

                    Text(
                        text = dateFormat.format(currentTime).uppercase(),
                        fontSize = 11.sp,
                        fontWeight = FontWeight.Bold,
                        color = Color(0xFF9CA3AF),
                        letterSpacing = 1.5.sp
                    )

                    Spacer(modifier = Modifier.height(24.dp))

                    // Active Focus Deliverable Card
                    if (activeLead != null || currentFocusTask != null) {
                        val displayTaskTitle = activeLead?.projectName?.ifEmpty { null }
                            ?: currentFocusTask?.title?.ifEmpty { null }
                            ?: "Creative Asset Sprint"
                        val displaySubtext = if (activeLead != null) {
                            "${activeLead.designerName} • ${activeLead.formattedElapsed ?: "In Progress"}${if (!activeLead.machineName.isNullOrBlank()) " • ${activeLead.machineName}" else ""}"
                        } else {
                            currentFocusTask?.safeBrand?.ifEmpty { "SuamiSihat" } ?: "SuamiSihat"
                        }
                        val displayCategory = if (activeLead != null) "LIVE STUDIO WORKSTREAM" else "CURRENT SPRINT FOCUS"
                        val displayColor = if (activeLead != null) Color(0xFF10B981) else Color(0xFF0078D4)

                        Surface(
                            color = Color(0xFF111827).copy(alpha = 0.8f),
                            shape = RoundedCornerShape(12.dp),
                            border = androidx.compose.foundation.BorderStroke(1.dp, Color(0xFF1F2937)),
                            modifier = Modifier.fillMaxWidth(0.9f)
                        ) {
                            Row(
                                modifier = Modifier.padding(14.dp),
                                verticalAlignment = Alignment.CenterVertically
                            ) {
                                Box(
                                    modifier = Modifier
                                        .size(36.dp)
                                        .clip(RoundedCornerShape(8.dp))
                                        .background(displayColor),
                                    contentAlignment = Alignment.Center
                                ) {
                                    Icon(
                                        if (activeLead != null) Icons.Default.Computer else Icons.Default.Brush,
                                        contentDescription = null,
                                        tint = Color.White,
                                        modifier = Modifier.size(20.dp)
                                    )
                                }
                                Spacer(modifier = Modifier.width(12.dp))
                                Column {
                                    Text(
                                        text = displayCategory,
                                        fontSize = 8.5.sp,
                                        fontWeight = FontWeight.Bold,
                                        color = if (activeLead != null) Color(0xFF10B981) else Color(0xFF38BDF8),
                                        letterSpacing = 1.sp
                                    )
                                    Text(
                                        text = displayTaskTitle,
                                        fontSize = 14.sp,
                                        fontWeight = FontWeight.Bold,
                                        color = Color.White,
                                        maxLines = 1
                                    )
                                    Text(
                                        text = displaySubtext,
                                        fontSize = 11.sp,
                                        color = Color(0xFF9CA3AF),
                                        maxLines = 1
                                    )
                                }
                            }
                        }
                    }
                }

                // Right Column: Analog Pomodoro Focus Dial
                Column(
                    modifier = Modifier.weight(0.8f),
                    horizontalAlignment = Alignment.CenterHorizontally,
                    verticalArrangement = Arrangement.Center
                ) {
                    Box(
                        modifier = Modifier
                            .size(190.dp)
                            .clickable { isTimerRunning = !isTimerRunning },
                        contentAlignment = Alignment.Center
                    ) {
                        Canvas(modifier = Modifier.fillMaxSize()) {
                            val strokeWidth = 8.dp.toPx()
                            // Track
                            drawCircle(
                                color = Color(0xFF1F2937),
                                style = Stroke(strokeWidth)
                            )
                            // Progress Arc
                            drawArc(
                                brush = Brush.sweepGradient(
                                    listOf(Color(0xFF0078D4), Color(0xFF38BDF8), Color(0xFFD4AF37))
                                ),
                                startAngle = -90f,
                                sweepAngle = pomoProgress * 360f,
                                useCenter = false,
                                style = Stroke(strokeWidth, cap = StrokeCap.Round)
                            )
                        }

                        Column(horizontalAlignment = Alignment.CenterHorizontally) {
                            Text(
                                text = String.format("%02d:%02d", pomoMinutes, pomoSeconds),
                                fontSize = 34.sp,
                                fontWeight = FontWeight.Bold,
                                color = Color.White,
                                letterSpacing = (-1).sp
                            )
                            Text(
                                text = if (isTimerRunning) "FOCUSING" else "PAUSED",
                                fontSize = 9.sp,
                                fontWeight = FontWeight.Bold,
                                color = if (isTimerRunning) Color(0xFF10B981) else Color(0xFFF59E0B),
                                letterSpacing = 1.2.sp
                            )
                        }
                    }

                    Spacer(modifier = Modifier.height(14.dp))

                    Text(
                        text = "Tap dial to pause • 25m Focus Block",
                        fontSize = 10.sp,
                        color = Color(0xFF6B7280)
                    )
                }
            }
        } else {
            // Portrait Layout: Stacked Layout
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(top = 50.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
                verticalArrangement = Arrangement.SpaceAround
            ) {
                // Top: Big Clock
                Column(horizontalAlignment = Alignment.CenterHorizontally) {
                    Row(verticalAlignment = Alignment.Bottom) {
                        Text(
                            text = timeHourMin.format(currentTime),
                            fontSize = 76.sp,
                            fontWeight = FontWeight.Light,
                            color = Color.White,
                            letterSpacing = (-3).sp
                        )
                        Text(
                            text = ":${timeSec.format(currentTime)}",
                            fontSize = 28.sp,
                            fontWeight = FontWeight.Light,
                            color = Color(0xFF38BDF8),
                            modifier = Modifier.padding(bottom = 10.dp, start = 4.dp)
                        )
                    }

                    Text(
                        text = dateFormat.format(currentTime).uppercase(),
                        fontSize = 11.sp,
                        fontWeight = FontWeight.Bold,
                        color = Color(0xFF9CA3AF),
                        letterSpacing = 1.5.sp
                    )
                }

                // Middle: Pomodoro Dial
                Box(
                    modifier = Modifier
                        .size(200.dp)
                        .clickable { isTimerRunning = !isTimerRunning },
                    contentAlignment = Alignment.Center
                ) {
                    Canvas(modifier = Modifier.fillMaxSize()) {
                        val strokeWidth = 10.dp.toPx()
                        drawCircle(
                            color = Color(0xFF1F2937),
                            style = Stroke(strokeWidth)
                        )
                        drawArc(
                            brush = Brush.sweepGradient(
                                listOf(Color(0xFF0078D4), Color(0xFF38BDF8), Color(0xFFD4AF37))
                            ),
                            startAngle = -90f,
                            sweepAngle = pomoProgress * 360f,
                            useCenter = false,
                            style = Stroke(strokeWidth, cap = StrokeCap.Round)
                        )
                    }

                    Column(horizontalAlignment = Alignment.CenterHorizontally) {
                        Text(
                            text = String.format("%02d:%02d", pomoMinutes, pomoSeconds),
                            fontSize = 38.sp,
                            fontWeight = FontWeight.Bold,
                            color = Color.White,
                            letterSpacing = (-1).sp
                        )
                        Text(
                            text = if (isTimerRunning) "FOCUS BLOCK" else "PAUSED",
                            fontSize = 10.sp,
                            fontWeight = FontWeight.Bold,
                            color = if (isTimerRunning) Color(0xFF10B981) else Color(0xFFF59E0B),
                            letterSpacing = 1.2.sp
                        )
                    }
                }

                // Bottom: Active Sprint / Live Studio Task Pill
                if (activeLead != null || currentFocusTask != null) {
                    val displayTaskTitle = activeLead?.projectName?.ifEmpty { null }
                        ?: currentFocusTask?.title?.ifEmpty { null }
                        ?: "Creative Asset Sprint"
                    val displaySubtext = if (activeLead != null) {
                        "${activeLead.designerName} • ${activeLead.formattedElapsed ?: "In Progress"}${if (!activeLead.machineName.isNullOrBlank()) " • ${activeLead.machineName}" else ""}"
                    } else {
                        currentFocusTask?.safeBrand?.ifEmpty { "SuamiSihat" } ?: "SuamiSihat"
                    }
                    val displayCategory = if (activeLead != null) "LIVE STUDIO WORKSTREAM" else "CURRENT SPRINT"
                    val displayColor = if (activeLead != null) Color(0xFF10B981) else Color(0xFF0078D4)

                    Surface(
                        color = Color(0xFF111827).copy(alpha = 0.9f),
                        shape = RoundedCornerShape(14.dp),
                        border = androidx.compose.foundation.BorderStroke(1.dp, Color(0xFF1F2937)),
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Row(
                            modifier = Modifier.padding(14.dp),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Box(
                                modifier = Modifier
                                    .size(36.dp)
                                    .clip(RoundedCornerShape(8.dp))
                                    .background(displayColor),
                                contentAlignment = Alignment.Center
                            ) {
                                Icon(
                                    if (activeLead != null) Icons.Default.Computer else Icons.Default.Assignment,
                                    contentDescription = null,
                                    tint = Color.White,
                                    modifier = Modifier.size(20.dp)
                                )
                            }
                            Spacer(modifier = Modifier.width(12.dp))
                            Column {
                                Text(
                                    text = displayCategory,
                                    fontSize = 8.5.sp,
                                    fontWeight = FontWeight.Bold,
                                    color = if (activeLead != null) Color(0xFF10B981) else Color(0xFF38BDF8),
                                    letterSpacing = 1.sp
                                )
                                Text(
                                    text = displayTaskTitle,
                                    fontSize = 14.sp,
                                    fontWeight = FontWeight.Bold,
                                    color = Color.White,
                                    maxLines = 1
                                )
                                Text(
                                    text = displaySubtext,
                                    fontSize = 11.sp,
                                    color = Color(0xFF9CA3AF),
                                    maxLines = 1
                                )
                            }
                        }
                    }
                }
            }
        }
    }
}
