from ursina import *
from ursina.prefabs.first_person_controller import FirstPersonController
import random

app = Ursina()

# 창 설정
window.fps_counter.enabled = False
window.exit_button.visible = False
window.title = "Granny: The Full House Escape 3D"

# 어두운 시야 및 안개 연출
scene.fog_color = color.black
scene.fog_density = 0.06

# --- [게임 상태 변수] ---
day = 1
max_days = 5
held_item = None  # 현재 들고 있는 아이템
is_hidden = False
granny_state = "PATROL"  # PATROL, INVESTIGATE, CHASE
noise_position = None
game_over = False
game_won = False

# --- [맵 및 가구 구축] ---
wall_color = color.rgb(40, 30, 25)

# 바닥 & 천장
ground = Entity(model='plane', scale=(40, 1, 40), color=color.dark_gray, collider='box')
ceiling = Entity(model='plane', scale=(40, 1, 40), y=6, rotation_x=180, color=color.black)

# 외벽
walls = [
    Entity(model='cube', scale=(40, 6, 1), position=(0, 3, 20), color=wall_color, collider='box'),
    Entity(model='cube', scale=(40, 6, 1), position=(0, 3, -20), color=wall_color, collider='box'),
    Entity(model='cube', scale=(1, 6, 40), position=(20, 3, 0), color=wall_color, collider='box'),
    Entity(model='cube', scale=(1, 6, 40), position=(-20, 3, 0), color=wall_color, collider='box'),
    # 방 분할 벽들
    Entity(model='cube', scale=(1, 6, 25), position=(-5, 3, 7.5), color=wall_color, collider='box'),
    Entity(model='cube', scale=(25, 6, 1), position=(7.5, 3, -5), color=wall_color, collider='box'),
    Entity(model='cube', scale=(1, 6, 15), position=(8, 3, 12.5), color=wall_color, collider='box'),
]

# --- [플레이어 & 카메라] ---
player = FirstPersonController(y=1.5, origin_y=-0.5, speed=4.5)
player.position = (-12, 1.5, -12)  # 스타트 침실 위치
flashlight = SpotLight(parent=camera, position=(0, 0, 0), color=color.rgb(255, 230, 180), angle=45)

# UI 텍스트
hud_text = Text(text="[E] 키를 눌러 서랍을 열거나 숨으세요.", position=(-0.85, 0.45), scale=1.2, color=color.white)
inventory_text = Text(text="손에 든 아이템: 없음", position=(-0.85, 0.40), scale=1.2, color=color.yellow)
day_text = Text(text=f"Day {day}", position=(0.7, 0.45), scale=2, color=color.red)

# --- [3중 잠금장치 정문] ---
main_door = Entity(model='cube', scale=(3.5, 5, 0.4), position=(0, 2.5, 19.6), color=color.rgb(60, 30, 10), collider='box')

# 정문 잠금 상태
lock_board = Entity(model='cube', scale=(3.8, 0.6, 0.6), position=(0, 2.5, 19.2), color=color.rgb(100, 60, 20), collider='box') # 판자
lock_wire = Entity(model='cube', scale=(0.1, 4.5, 0.1), position=(-1, 2.5, 19.2), color=color.cyan, collider='box') # 전선
lock_padlock = Entity(model='sphere', scale=0.6, position=(1, 2, 19.2), color=color.gold, collider='box') # 자물쇠

boards_broken = False
wire_cut = False
padlock_unlocked = False

# --- [서랍 & 아이템 배치] ---
items_to_place = ["망치", "절단기", "자물쇠 열쇠"]
random.shuffle(items_to_place)

drawers = [
    {"entity": Entity(model='cube', scale=(2, 1.2, 1), position=(-15, 0.6, 10), color=color.brown, collider='box'), "item": items_to_place[0], "opened": False},
    {"entity": Entity(model='cube', scale=(2, 1.2, 1), position=(15, 0.6, -15), color=color.brown, collider='box'), "item": items_to_place[1], "opened": False},
    {"entity": Entity(model='cube', scale=(2, 1.2, 1), position=(15, 0.6, 15), color=color.brown, collider='box'), "item": items_to_place[2], "opened": False},
]

# --- [장롱 숨기 장소 & 소음 화분] ---
closet = Entity(model='cube', scale=(2.5, 5, 1.5), position=(-17, 2.5, -10), color=color.rgb(30, 15, 5), collider='box')
vase = Entity(model='cylinder', scale=(0.6, 1, 0.6), position=(3, 1.5, -3), color=color.white, collider='box')
table = Entity(model='cube', scale=(3, 1, 3), position=(3, 0.5, -3), color=color.gray, collider='box')
vase_dropped = False

# --- [할머니 (Granny) AI] ---
granny = Entity(model='cube', scale=(1.3, 3, 1.3), position=(12, 1.5, 12), color=color.rgb(150, 0, 0), collider='box')
granny_eyes = Entity(parent=granny, model='sphere', scale=0.3, position=(0, 0.8, 0.5), color=color.yellow)

patrol_points = [(-12, 1.5, -12), (12, 1.5, 12), (-12, 1.5, 12), (12, 1.5, -12), (0, 1.5, 0)]
current_patrol_idx = 0

def make_noise(pos):
    global granny_state, noise_position
    granny_state = "INVESTIGATE"
    noise_position = pos
    hud_text.text = "소리가 났습니다! 그래니가 이쪽으로 오고 있습니다!"

def reset_day():
    global day, is_hidden, granny_state, game_over
    day += 1
    day_text.text = f"Day {day}"
    if day > max_days:
        game_over = True
        hud_text.text = "Day 5가 지나 최종 사망했습니다... Game Over"
        player.disable()
        mouse.locked = False
    else:
        player.position = (-12, 1.5, -12)
        granny.position = (12, 1.5, 12)
        granny_state = "PATROL"
        is_hidden = False
        camera.overlay.color = color.clear

# --- [키 입력 상호작용 (E키)] ---
def input(key):
    global held_item, is_hidden, boards_broken, wire_cut, padlock_unlocked, game_won

    if key == 'e' and not game_over and not game_won:
        # 1. 서랍 수색
        for d in drawers:
            if distance(player.position, d["entity"].position) < 2.5:
                if not d["opened"]:
                    d["opened"] = True
                    d["entity"].color = color.dark_gray
                    if d["item"]:
                        held_item = d["item"]
                        inventory_text.text = f"손에 든 아이템: [{held_item}]"
                        hud_text.text = f"서랍에서 [{held_item}]을(를) 획득했습니다!"
                        d["item"] = None
                else:
                    hud_text.text = "이미 수색한 서랍입니다."

        # 2. 장롱 속에 숨기 / 나오기
        if distance(player.position, closet.position) < 3.0:
            is_hidden = not is_hidden
            if is_hidden:
                player.position = closet.position
                hud_text.text = "장롱 속에 숨었습니다. (E키를 누르면 나갑니다)"
            else:
                player.position = closet.position + Vec3(0, 0, 3)
                hud_text.text = "장롱에서 나왔습니다."

        # 3. 정문 잠금 해제 작업
        if distance(player.position, main_door.position) < 3.5:
            if held_item == "망치" and not boards_broken:
                boards_broken = True
                lock_board.disable()
                held_item = None
                inventory_text.text = "손에 든 아이템: 없음"
                hud_text.text = "망치로 판자를 부수었습니다!"
            elif held_item == "절단기" and not wire_cut:
                wire_cut = True
                lock_wire.disable()
                held_item = None
                inventory_text.text = "손에 든 아이템: 없음"
                hud_text.text = "절단기로 경보선을 잘랐습니다!"
            elif held_item == "자물쇠 열쇠" and not padlock_unlocked:
                padlock_unlocked = True
                lock_padlock.disable()
                held_item = None
                inventory_text.text = "손에 든 아이템: 없음"
                hud_text.text = "자물쇠를 열었습니다!"
            
            # 최종 탈출 체크
            if boards_broken and wire_cut and padlock_unlocked:
                game_won = True
                hud_text.text = "모든 잠금장치를 해제하고 탈출에 성공했습니다!!"
                player.disable()
                mouse.locked = False

# --- [매 프레임 게임 루프] ---
def update():
    global vase_dropped, granny_state, current_patrol_idx, game_over

    if game_over or game_won:
        return

    # 화분 건드려 떨어뜨리기 (소음 발생)
    if not vase_dropped and distance(player.position, vase.position) < 1.2:
        vase_dropped = True
        vase.y = 0.3
        vase.rotation_z = 90
        make_noise(vase.position)

    # 시야 거리 체크 (시야 내 플레이어 감지)
    dist_to_player = distance(granny.position, player.position)

    if not is_hidden and dist_to_player < 12.0:
        granny_state = "CHASE"
    elif is_hidden and granny_state == "CHASE":
        granny_state = "PATROL"

    # --- [그래니 AI 행동] ---
    if granny_state == "CHASE":
        granny.look_at(player.position)
        granny.rotation_x = 0
        granny.position += granny.forward * time.dt * 4.2  # 빠르게 돌진

    elif granny_state == "INVESTIGATE":
        if noise_position:
            granny.look_at(noise_position)
            granny.rotation_x = 0
            granny.position += granny.forward * time.dt * 2.8
            if distance(granny.position, noise_position) < 1.5:
                granny_state = "PATROL"

    elif granny_state == "PATROL":
        target = patrol_points[current_patrol_idx]
        granny.look_at(target)
        granny.rotation_x = 0
        granny.position += granny.forward * time.dt * 2.2
        if distance(granny.position, target) < 2.0:
            current_patrol_idx = (current_patrol_idx + 1) % len(patrol_points)

    # 잡혔을 때 처리
    if dist_to_player < 2.0 and not is_hidden:
        camera.overlay.color = color.rgba(200, 0, 0, 220)
        reset_day()

app.run()