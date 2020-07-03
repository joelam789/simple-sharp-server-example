-- --------------------------------------------------------
-- Host:                         127.0.0.1
-- Server version:               8.0.18 - MySQL Community Server - GPL
-- Server OS:                    Win64
-- HeidiSQL Version:             11.0.0.5919
-- --------------------------------------------------------

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET NAMES utf8 */;
/*!50503 SET NAMES utf8mb4 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;


-- Dumping database structure for p2p_game_cache
CREATE DATABASE IF NOT EXISTS `p2p_game_cache` /*!40100 DEFAULT CHARACTER SET utf8 */ /*!80016 DEFAULT ENCRYPTION='N' */;
USE `p2p_game_cache`;

-- Dumping structure for table p2p_game_cache.tbl_bet_record
CREATE TABLE IF NOT EXISTS `tbl_bet_record` (
  `bet_id` int(11) NOT NULL AUTO_INCREMENT,
  `bet_uuid` varchar(64) NOT NULL,
  `client_id` varchar(45) NOT NULL,
  `front_end` varchar(45) NOT NULL,
  `session_id` varchar(64) NOT NULL DEFAULT '',
  `server_code` varchar(45) NOT NULL,
  `table_code` varchar(45) NOT NULL,
  `shoe_code` varchar(45) NOT NULL,
  `round_number` int(11) NOT NULL,
  `merchant_code` varchar(64) NOT NULL,
  `currency_code` varchar(5) NOT NULL DEFAULT 'CNY',
  `player_id` varchar(64) NOT NULL,
  `bet_type` int(11) NOT NULL DEFAULT '0',
  `bet_pool` int(11) NOT NULL DEFAULT '0',
  `coins_per_credit` decimal(19,2) DEFAULT '0.00',
  `bets_per_line` decimal(19,2) DEFAULT '0.00',
  `betted_lines` int(11) DEFAULT '0',
  `bet_amount` decimal(19,4) DEFAULT '0.0000',
  `pay_amount` decimal(19,4) DEFAULT '0.0000',
  `bet_state` int(11) NOT NULL DEFAULT '0',
  `game_type` int(11) NOT NULL DEFAULT '0',
  `game_input` varchar(500) DEFAULT '',
  `game_result` varchar(500) NOT NULL DEFAULT '',
  `bet_time` datetime DEFAULT NULL,
  `settle_time` datetime DEFAULT NULL,
  PRIMARY KEY (`bet_id`),
  UNIQUE KEY `UNI_BET_UUID` (`bet_uuid`),
  KEY `IDX_FES_ID` (`front_end`),
  KEY `IDX_BET_STATE` (`bet_state`),
  KEY `IDX_BET_TIME` (`bet_time`),
  KEY `IDX_SETTLE_TIME` (`settle_time`),
  KEY `IDX_SERVER_CODE` (`server_code`),
  KEY `IDX_TABLE_CODE` (`table_code`),
  KEY `IDX_GAME_ROUND` (`table_code`,`shoe_code`,`round_number`),
  KEY `IDX_BET_TYPE` (`bet_type`),
  KEY `IDX_GAME_TYPE` (`game_type`),
  KEY `IDX_BET_POOL` (`bet_pool`)
) ENGINE=InnoDB AUTO_INCREMENT=136 DEFAULT CHARSET=utf8 COMMENT='NDB_TABLE=READ_BACKUP=1';

-- Data exporting was unselected.

-- Dumping structure for table p2p_game_cache.tbl_bo_session
CREATE TABLE IF NOT EXISTS `tbl_bo_session` (
  `session_id` varchar(64) NOT NULL,
  `account_id` varchar(64) NOT NULL,
  `merchant_code` varchar(50) NOT NULL,
  `currency_code` varchar(5) NOT NULL DEFAULT 'CNY',
  `last_access_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`session_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='NDB_TABLE=READ_BACKUP=1 backoffice sessions';

-- Data exporting was unselected.

-- Dumping structure for table p2p_game_cache.tbl_player_session
CREATE TABLE IF NOT EXISTS `tbl_player_session` (
  `session_id` varchar(64) NOT NULL,
  `merchant_code` varchar(64) DEFAULT NULL,
  `currency_code` varchar(5) DEFAULT NULL,
  `player_id` varchar(64) DEFAULT NULL,
  `client_token` varchar(64) DEFAULT NULL,
  `update_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`session_id`),
  KEY `IDX_PLAYER_ID` (`player_id`),
  KEY `IDX_MERCHANT` (`merchant_code`),
  KEY `IDX_UPDATE_TIME` (`update_time`),
  KEY `IDX_CURRENCY` (`currency_code`),
  KEY `IDX_CLIENT_TOKEN` (`client_token`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='NDB_TABLE=READ_BACKUP=1';

-- Data exporting was unselected.

-- Dumping structure for table p2p_game_cache.tbl_round_state
CREATE TABLE IF NOT EXISTS `tbl_round_state` (
  `state_id` int(11) NOT NULL AUTO_INCREMENT,
  `game_type` int(11) NOT NULL DEFAULT '0',
  `table_type` int(11) NOT NULL DEFAULT '0',
  `server_code` varchar(45) NOT NULL,
  `table_code` varchar(45) NOT NULL,
  `table_name` varchar(64) NOT NULL DEFAULT '',
  `shoe_code` varchar(45) NOT NULL,
  `round_number` int(11) NOT NULL,
  `round_state` int(11) NOT NULL,
  `round_state_text` varchar(45) DEFAULT NULL,
  `player_count` int(11) NOT NULL DEFAULT '0',
  `init_flag` int(11) DEFAULT '0',
  `backup_number` int(11) NOT NULL DEFAULT '0',
  `bet_time_countdown` int(11) DEFAULT '-1',
  `gaming_countdown` int(11) DEFAULT '-1',
  `next_game_countdown` int(11) DEFAULT '-1',
  `game_output` varchar(500) DEFAULT NULL,
  `game_result` varchar(500) DEFAULT NULL,
  `game_remark` varchar(1000) DEFAULT NULL,
  `round_start_time` datetime DEFAULT NULL,
  `round_update_time` datetime DEFAULT NULL,
  PRIMARY KEY (`state_id`),
  UNIQUE KEY `UNI_GAME_ROUND` (`table_code`,`shoe_code`,`round_number`),
  UNIQUE KEY `UNI_GAME_HOST` (`table_code`,`init_flag`),
  KEY `IDX_SERVER_CODE` (`server_code`),
  KEY `IDX_TABLE_CODE` (`table_code`),
  KEY `IDX_ROUND_STATE` (`round_state`),
  KEY `IDX_BACKUP_NUMBER` (`backup_number`)
) ENGINE=InnoDB AUTO_INCREMENT=131 DEFAULT CHARSET=utf8 COMMENT='NDB_TABLE=READ_BACKUP=1';

-- Data exporting was unselected.

/*!40101 SET SQL_MODE=IFNULL(@OLD_SQL_MODE, '') */;
/*!40014 SET FOREIGN_KEY_CHECKS=IF(@OLD_FOREIGN_KEY_CHECKS IS NULL, 1, @OLD_FOREIGN_KEY_CHECKS) */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
